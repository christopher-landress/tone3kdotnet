using System.Text.Json;
using System.Text.Json.Serialization;
using Tone3000.Auth;
using Tone3000.Models;

namespace Tone3000.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(Tone))]
[JsonSerializable(typeof(Model))]
[JsonSerializable(typeof(Favorite))]
[JsonSerializable(typeof(PagedResult<PublicUser>))]
[JsonSerializable(typeof(PagedResult<Tone>))]
[JsonSerializable(typeof(PagedResult<Model>))]
[JsonSerializable(typeof(PagedResult<PublicMake>))]
[JsonSerializable(typeof(PagedResult<PublicTag>))]
[JsonSerializable(typeof(DataList<Tone>))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(DeviceAuthorization))]
[JsonSerializable(typeof(OAuthErrorBody))]
[JsonSerializable(typeof(Tone3000Tokens))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class Tone3000JsonContext : JsonSerializerContext;
