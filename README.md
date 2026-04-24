# TraderAlgoAPI

ASP.NET Core backend API for algorithmic trading — collects K-line data from Binance, serves it to an Angular frontend, and integrates with Kronos for AI-powered candle forecasting.

**Stack:** C# · .NET 10 · ASP.NET Core · Entity Framework Core · PostgreSQL (Supabase) · Binance API

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Vercel                               │
│                   Angular Frontend                          │
└──────────────────────────┬──────────────────────────────────┘
                           │ HTTP
┌──────────────────────────▼──────────────────────────────────┐
│                        Render                               │
│               TraderAlgoAPI  (C# .NET)                      │
│                                                             │
│   ┌─────────────────┐          ┌────────────────────────┐   │
│   │   Supabase      │          │  Kronos Connector API  │   │
│   │   PostgreSQL    │          │  (HTTP calls out)      │   │
│   └─────────────────┘          └────────────┬───────────┘   │
└────────────────────────────────────────────┼────────────────┘
                                             │ HTTP
┌────────────────────────────────────────────▼────────────────┐
│                   Modal.com (recommended)                   │
│                   Kronos Connector Service                  │
│                   (FastAPI + KronosPredictor)               │
└─────────────────────────────────────────────────────────────┘
```

| Layer | Technology | Hosting |
|---|---|---|
| Frontend | Angular | Vercel |
| Backend API | C# .NET | Render |
| Database | PostgreSQL | Supabase |
| Forecast Service | FastAPI + Kronos | Modal.com |

---

## Kronos Integration

### What is Kronos?

Kronos is an open-source foundation model for financial time series forecasting, trained on K-line (candlestick) data from 45+ global exchanges. It uses a hierarchical token-based autoregressive Transformer to predict future OHLCV (Open, High, Low, Close, Volume) candles from historical data.

Think of it as a GPT-style model, but instead of predicting the next word, it predicts the next candle.

**Stack:** Python · PyTorch · Flask (Web UI) · Hugging Face Hub · Pandas

### Available Models

| Model | Params | Context |
|---|---|---|
| kronos-mini | 4.1M | 2048 candles |
| kronos-small | 24.7M | 512 candles |
| kronos-base | 102.3M | 512 candles |

### Core API (`KronosPredictor`)

| Method | Description |
|---|---|
| `predict(df, x_timestamp, y_timestamp, pred_len)` | Single-series forecast — takes a DataFrame of historical candles, returns a DataFrame of predicted candles |
| `predict_batch(df_list, ...)` | Parallel multi-symbol prediction |
| `generate(x, x_stamp, y_stamp, ...)` | Low-level autoregressive generation with temperature/top-p/top-k sampling |

**Input:** DataFrame with columns `open`, `high`, `low`, `close`, `volume`, `amount` + datetime index

**Output:** DataFrame of predicted future candles, indexed by forecast timestamps

### Web UI (Flask — `webui/app.py`)

| Endpoint | Purpose |
|---|---|
| `POST /api/load-data` | Load a CSV/feather file |
| `POST /api/predict` | Run a forecast, returns predictions + Plotly chart |
| `POST /api/load-model` | Load a model by ID from HuggingFace |
| `GET /api/available-models` | List models with specs |
| `GET /api/model-status` | Current loaded model status |

### Lower-level Building Blocks

- **KronosTokenizer** — Encodes OHLCV data into discrete tokens (Binary Spherical Quantization); `.encode()` / `.decode()`
- **Kronos** — Raw Transformer model; `.decode_s1()` / `.decode_s2()` for hierarchical autoregressive inference
- **Finetuning pipeline** — `finetune/` (Qlib/Chinese market) and `finetune_csv/` (generic CSV) for adapting to your own data

### Integration Points with TraderAlgoAPI

Three natural integration points:

1. **Data ingestion** — Replace current data sources with calls to TraderAlgoAPI's historical candle endpoints. The adapter converts the API response into a pandas DataFrame with the required column names.
2. **Signal output** — After `predict()` returns forecast candles, POST the signals (predicted direction, magnitude, confidence from `sample_count`) to TraderAlgoAPI for order management.
3. **Real-time loop** — TraderAlgoAPI pushes new candle closes → Kronos runs inference → signals are returned. The sliding context window in `generate()` is already built for this pattern.

---

## Kronos Connector Setup

### Where to Host Kronos

You don't host Kronos directly — you host the **Kronos Connector**, which wraps Kronos. Hosting platform matters because Kronos is a heavy PyTorch model.

**Recommendation: Modal.com**

| Platform | Pros | Cons |
|---|---|---|
| **Modal.com** ✅ | Serverless GPU, pay-per-second, Python-native, zero ops | Cold starts ~5–10s |
| Render | Same platform as your backend, simple | CPU only, inference is very slow |
| Hugging Face Spaces | Free, ML-native | Less control, slow free tier |
| RunPod | Cheap persistent GPU | More DevOps overhead |

Modal is purpose-built for this: running Python ML workloads on demand. You deploy a Python function, it runs on GPU, you pay only when it runs. No server to manage.

### Separate Project vs Modifying Kronos

Create a **separate project**: `kronos-connector`

Add the original Kronos repo as a git submodule inside it — never modify files inside `kronos/`, treat it as a read-only dependency:

```
kronos-connector/               ← your new repo
├── kronos/                     ← git submodule (original repo, untouched)
│   ├── model/
│   │   ├── kronos.py
│   │   └── module.py
│   └── ...
├── app/
│   ├── main.py                 ← FastAPI app
│   ├── predictor.py            ← wraps KronosPredictor
│   └── schemas.py              ← request/response models
├── requirements.txt
└── modal_deploy.py             ← Modal deployment config
```

### Getting Upstream Updates

With a git submodule you keep access to upstream updates with one command:

```bash
cd kronos && git pull origin master && cd ..
git add kronos
git commit -m "update kronos submodule"
```

Your connector code imports from the submodule and is never touched by upstream changes:

```python
# app/predictor.py
import sys
sys.path.insert(0, "./kronos")
from model import KronosPredictor
```

### Step-by-Step Setup

**Step 1 — Create the connector repo**

```bash
mkdir kronos-connector && cd kronos-connector
git init
git submodule add https://github.com/ORIGINAL/Kronos.git kronos
```

**Step 2 — Create a minimal FastAPI wrapper**

```python
# app/main.py
from fastapi import FastAPI
from app.schemas import PredictRequest, PredictResponse
from app.predictor import run_prediction

app = FastAPI()

@app.post("/predict", response_model=PredictResponse)
async def predict(req: PredictRequest):
    return run_prediction(req)
```

**Step 3 — Deploy to Modal**

```python
# modal_deploy.py
import modal

image = modal.Image.debian_slim().pip_install("torch", "pandas", "huggingface_hub")
app = modal.App("kronos-connector")

@app.function(image=image, gpu="T4")
@modal.web_endpoint(method="POST")
def predict(request: dict):
    ...
```

**Step 4** — Point TraderAlgoAPI at the Modal endpoint URL and call it like any REST API from C#.

### Summary

| Question | Answer |
|---|---|
| Where to host Kronos? | Don't host Kronos — host the connector on Modal.com |
| Do I need to host it? | Yes — C# can't call Python directly |
| Where to host connector? | Modal.com (GPU, serverless) or Render (CPU, simpler) |
| Separate project? | Yes — new repo `kronos-connector` |
| Modify Kronos? | Never — use it as a git submodule |
| Get upstream updates? | Yes — `git submodule update --remote` |
