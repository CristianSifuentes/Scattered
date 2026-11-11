using System.Text.Json;

namespace Scattered.Shared.Secrets.Extensions;

public static class ObjectExtensions
{
    public static T ToObject<T>(this IDictionary<string, object> source) where T : new()
    {
        var someObject = new T();
        var someObjectType = someObject.GetType();

        foreach (var item in source)
        {
            var property = someObjectType.GetProperty(item.Key)!;
            // VaultSharp returns the secret values as JsonElement
            object? value = item.Value is JsonElement jsonElement
                ? jsonElement.Deserialize(property.PropertyType)
                : item.Value;
            property.SetValue(someObject, value, null);
        }
        return someObject;
    }
}