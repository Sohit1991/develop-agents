using Microsoft.Extensions.VectorData;
using System;
using System.Collections.Generic;
using System.Text;

namespace QdranntVectorStore
{
    public record ArchitectureDecision
    {
        [VectorStoreKey]
        public Guid DocumentId { get; set; } = Guid.NewGuid();

        [VectorStoreData]
        public string Title { get; set; } = string.Empty;

        [VectorStoreData]
        public string Content { get; set; } = string.Empty;

        // The 1536-dimentional arrat representing the semantic meaning of the content
        [VectorStoreVector(1536)]
        public ReadOnlyMemory<float> ContentVector { get; set; }
    }
}
