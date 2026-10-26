namespace Hermes.Channels.Tests;

/// <summary>
/// The worked example of RFC 8291 section 5 and its intermediate values of appendix A, each
/// base64url without padding as the RFC prints it, held as literals so the encryption is
/// checked against the standard (<c>02</c> MSG-PUSH-001; <c>07</c> MSG-ENG-001).
/// </summary>
internal static class Vectors
{
    internal const string Plaintext = "When I grow up, I want to be a watermelon";

    internal const string Message = "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    internal const string AuthenticationSecret = "BTBZMqHH6r4Tts7J_aSIgg";

    internal const string ReceiverPrivateKey = "q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94";

    internal const string ReceiverPublicKey = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";

    internal const string SenderPublicKey = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";

    internal const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";

    internal const string Ikm = "S4lYMb_L0FxCeq0WhDx813KgSYqU26kOyzWUdsXYyrg";

    internal const string Prk = "09_eUZGrsvxChDCGRCdkLiDXrReGOEVeSCdCcPBSJSc";

    internal const string Cek = "oIhVW04MRdy2XN9CiKLxTg";

    internal const string Nonce = "4h_95klXJ5E_qnoN";
}