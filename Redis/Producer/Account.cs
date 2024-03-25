using System.Text.Json.Serialization;
using NRedisStack.Search;

namespace Producer;

public record Account(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string CustomerName,
    [property: JsonPropertyName("department")] string CustomerDepartment,
    [property: JsonPropertyName("iban")] string Iban,
    [property: JsonPropertyName("balance")] decimal Balance,
    [property: JsonPropertyName("joinDate")] DateTimeOffset JoinDate)
{
    public static Schema Schema => new Schema()
        .AddNumericField(new FieldName("$.id", "id"))
        .AddTextField(new FieldName("$.name", "name"), sortable: true)
        .AddTextField(new FieldName("$.department", "department"), sortable: true)
        .AddTextField(new FieldName("$.iban", "iban"), sortable: true)
        .AddNumericField(new FieldName("$.balance", "balance"), sortable: true)
        .AddNumericField(new FieldName("$.joinDate", "joinDate"), sortable: true);
}