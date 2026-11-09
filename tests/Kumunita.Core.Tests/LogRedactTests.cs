using Kumunita.Core.Logging;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The P-3 <see cref="LogRedact"/> source-side redaction contract
/// (IMPROVE-SEC IS06, design §2.3). A **pure** unit — no
/// <c>PostgresFixture</>, no Marten: the two static helpers only. The P-3 rule
/// is: the log carries the domain (or a hash), **never** the local part of an
/// address, and nothing is retained.
/// </summary>
public class LogRedactTests
{
    [Fact]
    public void EmailDomain_ReturnsDomainOnly()
    {
        Assert.Equal("example.test", LogRedact.EmailDomain("a@example.test"));
        Assert.Equal("example.test", LogRedact.EmailDomain("A@Example.TEST")); // lower-cased

        // The local part is never in the result for a known address.
        string result = LogRedact.EmailDomain("resident.one+tag@Example.TEST");
        Assert.Equal("example.test", result);
        Assert.DoesNotContain("resident", result, System.StringComparison.Ordinal);
        Assert.DoesNotContain("tag", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public void EmailDomain_NullOrMalformed_IsNone()
    {
        Assert.Equal("(none)", LogRedact.EmailDomain(null));
        Assert.Equal("(none)", LogRedact.EmailDomain(""));
        Assert.Equal("(none)", LogRedact.EmailDomain("no-at-sign"));
        Assert.Equal("(none)", LogRedact.EmailDomain("@example.test")); // empty local part
        Assert.Equal("(none)", LogRedact.EmailDomain("a@"));           // empty domain
    }

    [Fact]
    public void Hash8_IsStableAndShort()
    {
        string first = LogRedact.Hash8("a@example.test");

        // Exactly 8 lowercase-hex characters.
        Assert.Equal(8, first.Length);
        foreach (char c in first)
            Assert.True((c is >= '0' and <= '9') || (c is >= 'a' and <= 'f'),
                $"expected lowercase hex, got '{c}'");

        // Stable: same input → same value.
        Assert.Equal(first, LogRedact.Hash8("a@example.test"));

        // Different input → different value.
        Assert.NotEqual(first, LogRedact.Hash8("b@example.test"));

        // Total: null / empty → the pinned sentinel "(none)".
        Assert.Equal("(none)", LogRedact.Hash8(null));
        Assert.Equal("(none)", LogRedact.Hash8(""));
    }
}
