# Gemini AI Integration

Pasukhi now supports **Google Gemini** as an alternative to OpenAI, with a generous free tier.

## Quick Start

### 1. Get a Gemini API Key

1. Visit https://aistudio.google.com/apikey
2. Sign in with your Google account
3. Click "Create API Key"
4. Copy the key

### 2. Configure Pasukhi

**appsettings.json** (production):
```json
{
  "AI": {
    "Provider": "Gemini",
    "ApiKey": "your-gemini-api-key-here",
    "Model": "gemini-2.0-flash-lite",
    "MaxTokens": 500,
    "Temperature": 0.3,
    "RequestTimeoutSeconds": 30
  }
}
```

**appsettings.Development.json** (local dev):
```json
{
  "AI": {
    "Provider": "Gemini",
    "ApiKey": "your-gemini-api-key-here",
    "Model": "gemini-2.0-flash-lite"
  }
}
```

### 3. Azure App Service Configuration

Set these environment variables:

| Name | Value |
|------|-------|
| `AI__Provider` | `Gemini` |
| `AI__ApiKey` | `your-key-here` |
| `AI__Model` | `gemini-2.0-flash-lite` (optional) |

---

## Available Models

| Model | Speed | Intelligence | Free Tier Limit |
|-------|-------|--------------|-----------------|
| `gemini-2.0-flash-lite` | Fastest | Good | 15 RPM, 1M tokens/day |
| `gemini-2.0-flash` | Fast | Better | 15 RPM, 1M tokens/day |
| `gemini-2.0-pro` | Slower | Best | 15 RPM, 1M tokens/day |

**Recommendation:** Start with `gemini-2.0-flash-lite` for FAQ/customer service. Upgrade to `flash` or `pro` only if you need better reasoning.

---

## Free Tier Limits

Gemini's free tier includes:
- **15 requests per minute** (RPM)
- **1 million tokens per day**
- **1,500 requests per day**

This is enough for ~500-1000 customer conversations per day depending on message length.

---

## Switching Back to OpenAI

Change the provider in configuration:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "Model": "gpt-5-mini"
  }
}
```

The rest of the pipeline (FAQ matching, rules, safety checks, escalation) works identically regardless of provider.

---

## How It Works

The `IAiService` abstraction allows switching providers without changing business logic:

```
InboundMessageConsumer
    ↓
IAiPromptBuilder → builds context
    ↓
IAiService → GeminiService OR OpenAiService
    ↓
IAiSafetyChecker → validates response
    ↓
send reply OR escalate
```

---

## Troubleshooting

### "Gemini API key is not configured"
Check that `AI__ApiKey` environment variable is set.

### "Gemini returned HTTP 400"
Usually means invalid model name or malformed request. Check logs.

### "Gemini returned HTTP 429"
Rate limit exceeded. You've hit the 15 RPM limit. Consider upgrading to paid tier or adding request queuing.

### Responses are too verbose
Lower the `Temperature` (try 0.1-0.2) or add stricter instructions to the business prompt.

---

## Cost Comparison

| Provider | Model | Cost per 1M tokens |
|----------|-------|-------------------|
| Gemini | 2.0-flash-lite | **Free** |
| Gemini | 2.0-flash | Free |
| Gemini | 2.0-pro | Free |
| OpenAI | gpt-5-mini | ~$2.50 |
| OpenAI | gpt-4.1-mini | ~$3.00 |

For a small business with 100 conversations/day:
- **Gemini**: $0/month
- **OpenAI**: ~$20-50/month
