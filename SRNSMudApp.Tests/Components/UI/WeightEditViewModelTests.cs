namespace SRNSMudApp.Tests.Components.UI;

using SRNSMudApp.Components.UI;

using Xunit;

public class WeightEditViewModelTests
{
    [Fact]
    public void DefaultConstructor_InitializesWithZeroWeight()
    {
        var vm = new WeightEditViewModel();
        Assert.Equal(0, vm.Weight);
        Assert.True(vm.CanSubmit);
    }

    [Fact]
    public void ParameterizedConstructor_InitializesWithGivenWeight()
    {
        var vm = new WeightEditViewModel(42);
        Assert.Equal(42, vm.Weight);
        Assert.True(vm.CanSubmit);
        Assert.Equal(42, vm.SubmitResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(-50, false)]
    public void CanSubmit_ValidatesNonNegative(int weight, bool expectedCanSubmit)
    {
        var vm = new WeightEditViewModel(weight);
        Assert.Equal(expectedCanSubmit, vm.CanSubmit);
    }
}