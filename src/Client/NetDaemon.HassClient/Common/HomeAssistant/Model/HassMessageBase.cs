using System.Collections;

namespace NetDaemon.Client.HomeAssistant.Model;

public record HassMessageBase
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
}

public interface ICorrelated
{
    IComparable Key { get; }

}
public interface ICorrelatedRequest : ICorrelated {}
public interface ICorrelatedResponse : ICorrelated {}
public interface ICorrelated<out TId> : ICorrelated
    where TId : IEquatable<TId>, IComparable
{
    TId Id { get; }

    IComparable ICorrelated.Key => Id;
}

public interface ICorrelatedRequest<out TId> : ICorrelated<TId> ,ICorrelatedRequest
    where TId : IEquatable<TId>, IComparable
{
}

public interface ICorrelatedResponse<out TId> : ICorrelated<TId>, ICorrelatedResponse
    where TId : IEquatable<TId>, IComparable
{
}
