using Newtonsoft.Json.Linq;
using System.Reflection;

namespace NeoModLoader.utils.SerializedAssets
{
    /// <summary>
    /// Because delegates like worldaction cannot be serialized, this is used so you can serialize them
    /// </summary>
    [Serializable]
    public class SerializableAsset<A> where A : Asset, new()
    {
        /// <summary>
        /// the variables of the asset
        /// </summary>
        public Dictionary<string, object> Variables = new();
        /// <summary>
        /// takes delegates and variables from an asset and takes them to a serializable asset
        /// </summary>
        public static void Serialize(A Asset, SerializableAsset<A> asset)
        {
            foreach (FieldInfo field in typeof(A).GetFields())
            {
                object Value = field.GetValue(Asset);
                if (Value is Delegate value)
                {
                    asset.Variables.Add(field.Name, value.AsString(false));
                }
                else
                {
                    asset.Variables.Add(field.Name, Value);
                }
            }
        }
        /// <summary>
        /// Converts the augmentation asset to a serializable version
        /// </summary>
        public static SerializableAsset<A> FromAsset(A Asset)
        {
            SerializableAsset<A> asset = new();
            Serialize(Asset, asset);
            return asset;
        }
        /// <summary>
        /// takes delegates and variables from a serializable asset and takes them to a asset
        /// </summary>
        public static void Deserialize(SerializableAsset<A> Asset, A asset)
        {
            static object GetRealValueOfObject(object Value, Type Type)
            {
                if (typeof(Delegate).IsAssignableFrom(Type))
                {
                    return (Value as string).AsDelegate(Type);
                }
                if (Type == typeof(int))
                {
                    return Convert.ToInt32(Value);
                }
                else if (Type == typeof(float))
                {
                    return Convert.ToSingle(Value);
                }
                else if(Type == typeof(Enum))
                {
                    return Enum.ToObject(Type, Convert.ToInt32(Value));
                }
                else if (Value is JObject JObject)
                {
                    return JObject.ToObject(Type);
                }
                return Value;
            }
            foreach (FieldInfo field in typeof(A).GetFields())
            {
                if (Asset.Variables.TryGetValue(field.Name, out object Value))
                {
                    field.SetValue(asset, GetRealValueOfObject(Value, field.FieldType));
                }
            }
        }
        /// <summary>
        /// converts the serializable version to its asset
        /// </summary>
        public static A ToAsset(SerializableAsset<A> Asset)
        {
            A asset = new();
            Deserialize(Asset, asset);
            return asset;
        }
    }
}