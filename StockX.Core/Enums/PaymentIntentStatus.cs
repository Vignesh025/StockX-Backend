using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace StockX.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentIntentStatus
{
    [EnumMember(Value = "PENDING")]
    Pending,

    [EnumMember(Value = "COMPLETED")]
    Completed,

    [EnumMember(Value = "FAILED")]
    Failed,

    [EnumMember(Value = "CANCELED")]
    Canceled
}

