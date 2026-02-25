using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace StockX.Core.Enums;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum UserRole
{
    [EnumMember(Value = "Admin")]
    Admin,

    [EnumMember(Value = "NormalUser")]
    NormalUser
}

