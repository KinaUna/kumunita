using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M7 U02 — the <see cref="PagedViewModel"/> <c>ForRoute</c> factory (ADR
/// 0090 D5). Pure record-shape tests — no database, no Testcontainers,
/// no <c>kw-l</c> resolution; the factory's contract is exactly what the
/// <c>_Pager</c> partial (U03/U04's drop-in) consumes.
/// <para>
/// Pins:
/// <list type="number">
/// <item><see cref="PagedViewModel.ForRoute"/> — <c>HasPrevious = page
/// &gt; 1</c>; <c>HasNext = hasMore</c> (D1 — the sole signal; there is no
/// <c>TotalPages</c>).</item>
/// <item>The <c>FilterParams</c> pass through verbatim (D7 — the pager's
/// hidden-input idiom).</item>
/// <item>The one-page case (<c>page = 1</c> + <c>hasMore = false</c>) is
/// the <see cref="PagedViewModel"/> shape that makes the <c>_Pager</c>
/// partial render nothing (F2 — a one-page surface shows no pager).</item>
/// </list>
/// </para>
/// </summary>
public class PagedViewModelTests
{
    [Fact(DisplayName = "ForRoute(page 1, full) — HasNext true, HasPrevious false, CurrentPage 1")]
    public void ForRoute_Page1_Full_HasNextTrue_HasPrevFalse()
    {
        var vm = PagedViewModel.ForRoute("/community/safety", page: 1, pageSize: 30, hasMore: true);

        Assert.Equal(1, vm.CurrentPage);
        Assert.Equal(30, vm.PageSize);
        Assert.True(vm.HasNext);
        Assert.False(vm.HasPrevious);
        Assert.Equal("/community/safety", vm.BaseUrl);
        Assert.Empty(vm.FilterParams);
    }

    [Fact(DisplayName = "ForRoute(page 2, partial) — HasNext false, HasPrevious true")]
    public void ForRoute_Page2_Partial_HasNextFalse_HasPrevTrue()
    {
        var vm = PagedViewModel.ForRoute("/community/safety", page: 2, pageSize: 30, hasMore: false);

        Assert.Equal(2, vm.CurrentPage);
        Assert.False(vm.HasNext);
        Assert.True(vm.HasPrevious);
    }

    [Fact(DisplayName = "ForRoute preserves FilterParams (D7 — the pager's hidden-input source)")]
    public void ForRoute_FilterParams_Preserved()
    {
        var filterParams = new Dictionary<string, string> { ["componentId"] = "safety" };
        var vm = PagedViewModel.ForRoute("/events", page: 2, pageSize: 30, hasMore: true, filterParams);

        Assert.Equal("safety", vm.FilterParams["componentId"]);
    }

    [Fact(DisplayName = "ForRoute(page 1, not full) — the one-page shape (F2: the _Pager partial renders nothing)")]
    public void ForRoute_Page1_NotFull_RendersNothing()
    {
        var vm = PagedViewModel.ForRoute("/events", page: 1, pageSize: 30, hasMore: false);

        // The <c>_Pager</c> partial's no-render condition (F2):
        //   @if (Model.HasPrevious || Model.HasNext) { … }
        // A one-page surface (page 1, hasMore false) makes both false —
        // the partial emits nothing.
        Assert.False(vm.HasNext);
        Assert.False(vm.HasPrevious);
    }
}
