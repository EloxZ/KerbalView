using SpaceWarp.API.Configuration;
using Newtonsoft.Json;

namespace EloxKerbalview
{
    [JsonObject(MemberSerialization.OptOut)]
    [ModConfig]
    public class EloxKerbalviewConfig
    {
         [ConfigField("up")] [ConfigDefaultValue(15)] public double up;
         [ConfigField("forward")] [ConfigDefaultValue(-15)] public double forward;
    }
}