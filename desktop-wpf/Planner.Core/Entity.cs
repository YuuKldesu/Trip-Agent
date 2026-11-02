using System;

namespace Planner.Core;

public abstract class Entity
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
}
