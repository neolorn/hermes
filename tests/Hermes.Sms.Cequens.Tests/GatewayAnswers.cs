using System.Globalization;

namespace Hermes.Sms.Cequens.Tests;

/// <summary>
/// The gateway's answers as Cequens's reference documents them (<c>developer.cequens.com/reference</c>,
/// "Sending SMS" and "Getting Account Balance"): their members, nesting and texts, with the
/// values a test needs in place of the reference's.
/// </summary>
internal static class GatewayAnswers
{
    /// <summary>The message's identifier in <see cref="Sent"/>.</summary>
    internal const string SmsId = "0461f8f4-4c4f-4dc0-b7ed-097f4f9f85f4";

    /// <summary>The send's 200, "Result".</summary>
    internal const string Sent = """
        {
          "replyCode": 0,
          "replyMessage": "Request handled successfully",
          "requestId": "2fbc0560-6187-11e7-b60b-77c0e21cef84",
          "clientRequestId": 0,
          "requestTime": "2017-07-05T15:38:10.742",
          "data": {
            "SentSMSIDs": [
              {
                "SMSId": "0461f8f4-4c4f-4dc0-b7ed-097f4f9f85f4"
              }
            ],
            "InvalidRecipients": ""
          }
        }
        """;

    /// <summary>The send's 200 with the number among the invalid recipients and no message made.</summary>
    internal const string SentToAnInvalidRecipient = """
        {
          "replyCode": 0,
          "replyMessage": "Request handled successfully",
          "requestId": "2fbc0560-6187-11e7-b60b-77c0e21cef84",
          "clientRequestId": 0,
          "requestTime": "2017-07-05T15:38:10.742",
          "data": {
            "SentSMSIDs": [],
            "InvalidRecipients": "447700900123"
          }
        }
        """;

    /// <summary>The send's 200 with no invalid recipient and no message made.</summary>
    internal const string SentWithoutAnIdentifier = """
        {
          "replyCode": 0,
          "replyMessage": "Request handled successfully",
          "requestId": "2fbc0560-6187-11e7-b60b-77c0e21cef84",
          "clientRequestId": 0,
          "requestTime": "2017-07-05T15:38:10.742",
          "data": {
            "SentSMSIDs": [],
            "InvalidRecipients": ""
          }
        }
        """;

    /// <summary>The 401, "Invalid credentials", as the live account answered it too.</summary>
    internal const string Unauthorized = """
        {
          "replyCode": -1,
          "replyMessage": "[Request failed] UnAuthorized-Invalid User",
          "requestId": "2c537560-d708-11ec-b801-870c53cf4e9d",
          "clientRequestId": 0,
          "requestTime": "2022-05-19T00:11:04.502",
          "error": {
            "status": 401,
            "description": "Not valid user credentials."
          }
        }
        """;

    /// <summary>A 400 with the error table's <paramref name="code"/> and its <paramref name="details"/>.</summary>
    internal static string Refused(int code, string details)
    {
        return string.Create(CultureInfo.InvariantCulture, $$"""
            {
              "replyCode": -1,
              "replyMessage": "[Request failed] Bad Request",
              "requestId": "45878cc0-6187-11e7-b60b-77c0e21cef84",
              "clientRequestId": 0,
              "requestTime": "2017-07-05T15:38:47.308",
              "error": {
                "status": 400,
                "description": "Parameters missing or invalid for requested resource or validations failed.",
                "internalErrors": [
                  {
                    "code": {{code}},
                    "details": "{{details}}"
                  }
                ]
              }
            }
            """);
    }

    /// <summary>The balance's 200, "Result", with <paramref name="credit"/> as the account's credit.</summary>
    internal static string Balance(decimal credit)
    {
        return string.Create(CultureInfo.InvariantCulture, $$"""
            {
              "replyCode": 0,
              "replyMessage": "string",
              "requestId": "string",
              "clientRequestId": 0,
              "requestTime": "2017-07-06T10:45:11.129Z",
              "data": {
                "Account": {
                  "UserName": "string",
                  "Credit": {{credit}}
                },
                "SubAccounts": [
                  {
                    "UserName": "string",
                    "Credit": 0,
                    "BalanceMode": "Allocated balance"
                  }
                ]
              }
            }
            """);
    }
}