using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Metadata.Models.Github;

public class GitCommitFile
{
     public string? Filename { get; init; }

    [JsonPropertyName("raw_url")]
    public string? RawUrl { get; init; }    
}

