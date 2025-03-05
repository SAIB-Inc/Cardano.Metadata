using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Models.Github;

public class GitCommitAuthor
{
    public string? Name { get; init; } 
    public string? Email { get; init; } 
    public DateTime? Date { get; init; }     
}

