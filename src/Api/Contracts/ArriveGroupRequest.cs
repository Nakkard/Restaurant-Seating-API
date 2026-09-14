using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed record ArriveGroupRequest([Range(1, 6)] int Size);
