using System.Text.Json.Serialization;
using Tone3000.Serialization;

namespace Tone3000.Models;

/// <summary>Kind of gear a tone captures.</summary>
[JsonConverter(typeof(WireEnumConverter<Gear>))]
public enum Gear
{
    /// <summary>A value this library does not recognise.</summary>
    Unknown = 0,
    /// <summary><c>amp</c></summary>
    [WireValue("amp")] Amp,
    /// <summary><c>amp-cab</c> (replaces the deprecated <c>full-rig</c>).</summary>
    [WireValue("amp-cab")] AmpCab,
    /// <summary><c>pedal</c></summary>
    [WireValue("pedal")] Pedal,
    /// <summary><c>outboard</c></summary>
    [WireValue("outboard")] Outboard,
    /// <summary><c>cab</c></summary>
    [WireValue("cab")] Cab,
    /// <summary><c>space</c></summary>
    [WireValue("space")] Space,
    /// <summary><c>experimental</c></summary>
    [WireValue("experimental")] Experimental,
}

/// <summary>File format of a tone's models.</summary>
[JsonConverter(typeof(WireEnumConverter<ToneFormat>))]
public enum ToneFormat
{
    /// <summary>A value this library does not recognise.</summary>
    Unknown = 0,
    /// <summary>Neural Amp Modeler (<c>nam</c>).</summary>
    [WireValue("nam")] Nam,
    /// <summary>Impulse response (<c>ir</c>).</summary>
    [WireValue("ir")] ImpulseResponse,
    /// <summary>AIDA-X (<c>aida-x</c>).</summary>
    [WireValue("aida-x")] AidaX,
    /// <summary>Amplitube/AA snapshot (<c>aa-snapshot</c>).</summary>
    [WireValue("aa-snapshot")] AaSnapshot,
    /// <summary>Proteus (<c>proteus</c>).</summary>
    [WireValue("proteus")] Proteus,
}

/// <summary>NAM model architecture version. Sent as the strings <c>1</c>, <c>2</c> or <c>custom</c>.</summary>
[JsonConverter(typeof(WireEnumConverter<Architecture>))]
public enum Architecture
{
    /// <summary>A value this library does not recognise.</summary>
    Unknown = 0,
    /// <summary>Architecture 1 (A1).</summary>
    [WireValue("1")] A1,
    /// <summary>Architecture 2 (A2).</summary>
    [WireValue("2")] A2,
    /// <summary>Custom architecture.</summary>
    [WireValue("custom")] Custom,
}

/// <summary>Model size.</summary>
[JsonConverter(typeof(WireEnumConverter<ModelSize>))]
public enum ModelSize
{
    /// <summary>A value this library does not recognise.</summary>
    Unknown = 0,
    /// <summary><c>standard</c></summary>
    [WireValue("standard")] Standard,
    /// <summary><c>lite</c></summary>
    [WireValue("lite")] Lite,
    /// <summary><c>feather</c></summary>
    [WireValue("feather")] Feather,
    /// <summary><c>nano</c></summary>
    [WireValue("nano")] Nano,
    /// <summary><c>custom</c></summary>
    [WireValue("custom")] Custom,
}

/// <summary>License a creator published a tone under.</summary>
[JsonConverter(typeof(WireEnumConverter<License>))]
public enum License
{
    /// <summary>A value this library does not recognise.</summary>
    Unknown = 0,
    /// <summary>TONE3000 license (<c>t3k</c>).</summary>
    [WireValue("t3k")] T3k,
    /// <summary><c>cc-by</c></summary>
    [WireValue("cc-by")] CcBy,
    /// <summary><c>cc-by-sa</c></summary>
    [WireValue("cc-by-sa")] CcBySa,
    /// <summary><c>cc-by-nc</c></summary>
    [WireValue("cc-by-nc")] CcByNc,
    /// <summary><c>cc-by-nc-sa</c></summary>
    [WireValue("cc-by-nc-sa")] CcByNcSa,
    /// <summary><c>cc-by-nd</c></summary>
    [WireValue("cc-by-nd")] CcByNd,
    /// <summary><c>cc-by-nc-nd</c></summary>
    [WireValue("cc-by-nc-nd")] CcByNcNd,
    /// <summary>Public domain; spelled <c>cco</c> in the API docs (likely CC0).</summary>
    [WireValue("cco")] Cc0,
}

/// <summary>Sort order for <c>/tones/search</c>.</summary>
[JsonConverter(typeof(WireEnumConverter<TonesSort>))]
public enum TonesSort
{
    /// <summary>Server default: best match with a query, otherwise trending.</summary>
    Default = 0,
    /// <summary><c>best-match</c></summary>
    [WireValue("best-match")] BestMatch,
    /// <summary><c>newest</c></summary>
    [WireValue("newest")] Newest,
    /// <summary><c>oldest</c></summary>
    [WireValue("oldest")] Oldest,
    /// <summary><c>trending</c></summary>
    [WireValue("trending")] Trending,
    /// <summary><c>downloads-all-time</c></summary>
    [WireValue("downloads-all-time")] DownloadsAllTime,
}

/// <summary>Sort order for <c>/users</c>.</summary>
[JsonConverter(typeof(WireEnumConverter<UsersSort>))]
public enum UsersSort
{
    /// <summary>Server default (<c>tones</c>).</summary>
    Default = 0,
    /// <summary><c>tones</c></summary>
    [WireValue("tones")] Tones,
    /// <summary><c>downloads</c></summary>
    [WireValue("downloads")] Downloads,
    /// <summary><c>favorites</c></summary>
    [WireValue("favorites")] Favorites,
    /// <summary><c>models</c></summary>
    [WireValue("models")] Models,
}

/// <summary>Sort order for <c>/makes</c> and <c>/tags</c>.</summary>
[JsonConverter(typeof(WireEnumConverter<TaxonomySort>))]
public enum TaxonomySort
{
    /// <summary>Server default (<c>tones</c>).</summary>
    Default = 0,
    /// <summary><c>tones</c></summary>
    [WireValue("tones")] Tones,
    /// <summary><c>name</c></summary>
    [WireValue("name")] Name,
}
