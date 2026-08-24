using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Xunit;

namespace DeveloperMemory.Api.Tests.Services;

public class TokenEstimatorTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abcd", 1)]
    [InlineData("abcdefgh", 2)]
    [InlineData("Hello, world!", 4)]
    public void EstimateTokens_ReturnsCorrectEstimate(string? text, int expected)
    {
        Assert.Equal(expected, TokenEstimator.EstimateTokens(text!));
    }

    [Fact]
    public void EstimateTokens_LongText_ReturnsNonZero()
    {
        var text = new string('x', 1000);
        var tokens = TokenEstimator.EstimateTokens(text);
        Assert.Equal(250, tokens); // 1000 / 4 = 250
    }

    [Fact]
    public void EstimateRequestTokens_NullRequest_ReturnsZero()
    {
        Assert.Equal(0, TokenEstimator.EstimateRequestTokens(null!));
    }

    [Fact]
    public void EstimateRequestTokens_EmptyMessages_ReturnsZero()
    {
        var request = new OpenAIChatCompletionRequest { Messages = new List<Message>() };
        Assert.Equal(0, TokenEstimator.EstimateRequestTokens(request));
    }

    [Fact]
    public void EstimateRequestTokens_SingleMessage_ReturnsNonZero()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Messages = new List<Message>
            {
                new Message { Role = "user", Content = "Hello, world!" }
            }
        };
        var tokens = TokenEstimator.EstimateRequestTokens(request);
        Assert.True(tokens > 0); // 4 (overhead) + 3 (12 chars / 4) = 7
    }

    [Fact]
    public void EstimateRequestTokens_MultipleMessages_SumsUp()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Messages = new List<Message>
            {
                new Message { Role = "system", Content = "You are helpful." },
                new Message { Role = "user", Content = "Hello!" }
            }
        };
        var tokens = TokenEstimator.EstimateRequestTokens(request);
        Assert.True(tokens > 7); // More than a single message
    }

    [Fact]
    public void EstimateResponseTokens_NullResponse_ReturnsZero()
    {
        Assert.Equal(0, TokenEstimator.EstimateResponseTokens(null!));
    }

    [Fact]
    public void EstimateResponseTokens_EmptyChoices_ReturnsZero()
    {
        var response = new OpenAIChatCompletionResponse { Choices = new List<Choice>() };
        Assert.Equal(0, TokenEstimator.EstimateResponseTokens(response));
    }

    [Fact]
    public void EstimateResponseTokens_WithContent_ReturnsNonZero()
    {
        var response = new OpenAIChatCompletionResponse
        {
            Choices = new List<Choice>
            {
                new Choice
                {
                    Message = new Message { Role = "assistant", Content = "Hello, how can I help?" }
                }
            }
        };
        var tokens = TokenEstimator.EstimateResponseTokens(response);
        Assert.True(tokens > 0);
    }
}
