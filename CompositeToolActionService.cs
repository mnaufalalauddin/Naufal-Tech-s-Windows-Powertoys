using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class CompositeToolActionService : IToolActionService
{
    private readonly IReadOnlyDictionary<string, (IToolActionService Service, ToolActionDefinition Definition)> _items;

    internal CompositeToolActionService(params IToolActionService[] services)
    {
        Dictionary<string, (IToolActionService, ToolActionDefinition)> items = new(StringComparer.OrdinalIgnoreCase);
        foreach (IToolActionService service in services)
        foreach (ToolActionDefinition action in service.GetActions())
        {
            if (!items.TryAdd(action.Id, (service, action)))
                throw new InvalidOperationException($"Duplicate action definition id: {action.Id}");
        }
        _items = items;
    }

    public IReadOnlyList<ToolActionDefinition> GetActions() => _items.Values.Select(item => item.Definition).ToArray();

    public Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
    {
        var item = Resolve(definition);
        return item.Service.RunAsync(item.Definition);
    }

    public Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition)
    {
        var item = Resolve(definition);
        return item.Service.RestoreAsync(item.Definition);
    }

    private (IToolActionService Service, ToolActionDefinition Definition) Resolve(ToolActionDefinition definition) =>
        _items.TryGetValue(definition.Id, out var item) ? item : throw new KeyNotFoundException(definition.Id);
}
