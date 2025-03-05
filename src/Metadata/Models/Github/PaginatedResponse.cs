using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Models.Github;

public class PaginatedResponse<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int Offset { get; init; }
    public int Limit { get; init; }
    public int TotalCount { get; init; }    
}
