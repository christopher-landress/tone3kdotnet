using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Tone3000.Auth;

namespace Tone3000.Tests;

public class MiscTests
{
    [Fact]
    public async Task Ir_content_hash_covers_only_the_data_chunk()
    {
        byte[] samples = [1, 2, 3, 4, 5, 6];
        using var wav = new MemoryStream();
        void Chunk(string id, byte[] data)
        {
            wav.Write(System.Text.Encoding.ASCII.GetBytes(id));
            Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(size, (uint)data.Length);
            wav.Write(size);
            wav.Write(data);
            if (data.Length % 2 == 1)
            {
                wav.WriteByte(0);
            }
        }

        wav.Write("RIFF\0\0\0\0WAVE"u8);
        Chunk("fmt ", new byte[16]);
        Chunk("LIST", [9, 9, 9]); // odd-sized chunk to exercise padding
        Chunk("data", samples);
        wav.Position = 0;

        var hash = await ContentHash.ForImpulseResponseAsync(wav);

        var expected = Convert.ToHexStringLower(SHA256.HashData([.. "ir-content-v1\0"u8, .. samples]));
        Assert.Equal("ir-content-v1:" + expected, hash);
    }

    [Fact]
    public async Task Nam_content_hash_reads_weights_hash()
    {
        using var nam = new MemoryStream("""{ "version": "0.5.4", "metadata": { "weights_hash": "nam-content-v1:abc123" }, "weights": [] }"""u8.ToArray());
        Assert.Equal("nam-content-v1:abc123", await ContentHash.ForNamAsync(nam));
    }

    [Fact]
    public async Task Ir_hash_rejects_non_wav()
    {
        using var junk = new MemoryStream(new byte[32]);
        await Assert.ThrowsAsync<InvalidDataException>(() => ContentHash.ForImpulseResponseAsync(junk));
    }

    [Fact]
    public async Task Dependency_injection_wires_client_auth_and_store()
    {
        var handler = new FakeHandler((_, _) => FakeHandler.Json("""
            { "id": 7, "username": "me", "is_verified": false, "url": "https://www.tone3000.com/me",
              "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-01-01T00:00:00Z" }
            """));
        var services = new ServiceCollection();
        services.AddSingleton<ITokenStore>(new InMemoryTokenStore(Fixtures.ValidTokens()));
        services.AddTone3000Client(o => o.ClientId = "t3k_pub_test")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<ITone3000Client>();
        var user = await client.GetCurrentUserAsync();

        Assert.Equal("me", user.Username);
        Assert.Equal("Bearer access-1", Assert.Single(handler.Requests).Authorization);
        Assert.NotNull(provider.GetRequiredService<Tone3000OAuthClient>());
        Assert.Same(provider.GetRequiredService<Tone3000TokenManager>(), provider.GetRequiredService<Tone3000TokenManager>());
    }

    [Fact]
    public void Only_tone3000_hosts_get_the_token()
    {
        Assert.True(Tone3000Client.IsTone3000Host(new Uri("https://www.tone3000.com/x")));
        Assert.True(Tone3000Client.IsTone3000Host(new Uri("https://tone3000.com/x")));
        Assert.False(Tone3000Client.IsTone3000Host(new Uri("https://tone3000.com.evil.net/x")));
        Assert.False(Tone3000Client.IsTone3000Host(new Uri("https://nottone3000.com/x")));
    }
}
