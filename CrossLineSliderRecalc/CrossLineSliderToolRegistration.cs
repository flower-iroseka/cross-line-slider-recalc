using Mapping_Tools.Application.Projects.Models;
using Mapping_Tools.Application.Tools;
using Mapping_Tools.Desktop.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace CrossLineSliderRecalc;

/// <summary>
///     Registers the Cross-line slider recalc plugin with the Mapping Tools desktop shell.
/// </summary>
[MappingToolDefinition]
public sealed class CrossLineSliderToolRegistration : IMappingToolDefinition
{
    /// <inheritdoc />
    public ToolScrollBarVisibility HorizontalScrollBarVisibility => ToolScrollBarVisibility.Disabled;

    /// <inheritdoc />
    public ToolScrollBarVisibility VerticalScrollBarVisibility => ToolScrollBarVisibility.Auto;

    /// <inheritdoc />
    public ToolDefinition Definition => CrossLineSliderToolDefinition.Definition;

    /// <inheritdoc />
    public ToolConfigSchema ConfigSchema => ToolConfigSchema.ForTool(Definition.Id);

    /// <inheritdoc />
    public Type ViewModelType => typeof(CrossLineSliderToolViewModel);

    /// <inheritdoc />
    public Type ViewType => typeof(CrossLineSliderToolView);

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<CrossLineSliderService>();
    }
}
