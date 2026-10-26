using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using Hermes.Core;

using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace Hermes.Channels;

/// <summary>
/// The shipped push provider (<c>02</c> MSG-PUSH-001): Web Push by RFC 8030, its messages
/// encrypted by RFC 8291 and signed by RFC 8292 with the VAPID key version the subscription
/// was made with (MSG-PUSH-007), through Lib.Net.Http.WebPush's client over the named client
/// <c>Hermes.Push</c>, with <c>AutoRetryAfter</c> off so a 429 comes back at once. Every
/// answer is classed as MSG-PUSH-005 gives it, and none of its text is kept.
/// </summary>
internal sealed class WebPushProvider(IHttpClientFactory clients, Func<string> subject, PushKeys keys, TimeProvider time) : IPushProvider
{
    public PushPlatform Platform => PushPlatform.WebPush;

    public async ValueTask<Attempt> SendAsync(PushRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request is not PushRequest.WebPush push)
        {
            throw new ArgumentException("The request is not for Web Push.", nameof(request));
        }

        // Read first, as it throws where the startup checks have not completed.
        string vapidSubject = subject();
        if (keys.Find(push.KeyVersion) is not { } key)
        {
            return new Attempt.Transient(AttemptReason.KeyUnavailable, null, null);
        }

        PushSubscription subscription = new() { Endpoint = push.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, push.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, push.Auth);

        using ReadOnlyMemoryContent content = new(push.Payload);
        PushMessage message = new(content)
        {
            TimeToLive = (int)push.Ttl.TotalSeconds,
            Urgency = Urgency(push.Urgency),
            Topic = push.Topic,
        };

        using VapidAuthentication authentication = new(key.PublicKey, key.PrivateKey) { Subject = vapidSubject, TokenCache = key.Tokens };
        using HttpClient client = clients.CreateClient(PushClient.Name);
        PushServiceClient service = new(client) { AutoRetryAfter = false };
        try
        {
            await service.RequestPushMessageDeliveryAsync(subscription, message, authentication, VapidAuthenticationScheme.Vapid, cancellationToken).ConfigureAwait(false);
            return new Attempt.Accepted(null);
        }
        catch (PushServiceClientException answer)
        {
            return PushAnswer.Class((int)answer.StatusCode, RetryAfter(answer.Headers));
        }
        catch (HttpRequestException)
        {
            return new Attempt.Transient(AttemptReason.ConnectionFailed, null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's timeout, Dispatch:Timeout, ended the call, not the caller.
            return new Attempt.Transient(AttemptReason.Timeout, null, null);
        }
    }

    private static PushMessageUrgency Urgency(PushUrgency urgency)
    {
        return urgency switch
        {
            PushUrgency.VeryLow => PushMessageUrgency.VeryLow,
            PushUrgency.Low => PushMessageUrgency.Low,
            PushUrgency.Normal => PushMessageUrgency.Normal,
            PushUrgency.High => PushMessageUrgency.High,
            _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Not an urgency of 08 section 2."),
        };
    }

    private TimeSpan? RetryAfter(HttpResponseHeaders? headers)
    {
        return headers?.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date > time.GetUtcNow() ? date - time.GetUtcNow() : TimeSpan.Zero,
            _ => null,
        };
    }
}