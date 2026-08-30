using DeveloperMemory.Application.DTOs;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Orchestrates agent-aware context retrieval.
/// Builds on the existing Phase-S retrieval pipeline by enriching the retrieval
/// request with agent-specific context signals (agent type, task intent, etc.).
/// 
/// Does NOT replace the existing retrieval pipeline — enriches it.
/// </summary>
public interface IAgentContextService
{
    /// <summary>
    /// Retrieves context-aware memories for an agent request.
    /// Resolves agent context, builds a RetrievalRequest, and delegates to
    /// the existing MemoryRetrievalService.
    /// </summary>
    Task<AgentContextResult> RetrieveContextAsync(
        AgentContextRetrievalRequest request,
        string ownerId,
        CancellationToken ct = default);
}
