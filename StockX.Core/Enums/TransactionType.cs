using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace StockX.Core.Enums;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum TransactionType
{
    [EnumMember(Value = "DEPOSIT")]
    Deposit,

    [EnumMember(Value = "STOCK_BUY")]
    StockBuy,

    [EnumMember(Value = "STOCK_SELL")]
    StockSell
}

