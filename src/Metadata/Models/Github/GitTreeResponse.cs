using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Models.Github;

public class GitTreeResponse
{
    public GitTreeItem[]? Tree { get; init; }
}