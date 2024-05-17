# Small Language Model Hosting with ONNX Runtime and HuggingFace TextGenerationAPI
This repository provides a solution for hosting a Small Language Model, specifically PHI-3, using ONNX Runtime. It also includes a HuggingFace TextGenerationAPI compatible WebAPI layer for easy integration and usage.

## Overview
The solution is designed for offline development and testing of applications that use SemanticKernel. It allows for efficient and offline language model hosting, which can be particularly useful in environments with limited or no internet access.

## Components

* ONNX Runtime: An open-source performance-focused engine for running machine learning models. More details can be found [here](https://onnx.ai/onnx-runtime).
* PHI-3: A small language model that can be used for a variety of NLP tasks. More details can be found [here](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct).
* HuggingFace TextGenerationAPI: An API for generating text using HuggingFace's transformers. More details can be found [here](https://huggingface.co/docs/text-generation-inference/basic_tutorials/consuming_tgi).
* SemanticKernel: A tool for semantic search and text similarity. More details can be found [here](https://github.com/microsoft/semantic-kernel).

## Preparations

* Clone this repository
* Download the language model you would like to use
  * Check that GIT LFS was installed and active ahead of cloning the model repository
  * e.g. `git clone https://huggingface.co/microsoft/Phi-3-mini-4k-instruct`
* Download the text encoding model you would like to use
  * e.g. `git clone https://huggingface.co/CompVis/stable-diffusion-v1-4 -b onnx`
* Download the tokenizer model you would like to use
  * e.g. `curl -# -O -L https://github.com/cassiebreviu/StableDiffusion/raw/main/StableDiffusion.ML.OnnxRuntime/cliptokenizer.onnx`
* Edit [appsettings.json](appsettings.json) with 
  * `SmallLanguageModelPath` - full path to the folder with the Onnx model you want to use
  * `SmallLanguageModelSystemPrompt` - system prompt to be used by the application
  * `TextEncoderModelPath` - full path to the Onnx model file for text encoding
  * `TokenizerModelPath` - full path to the Onnx model file for tokenizing
* Run it, either
  * `dotnet run -c Release onnx-huggingfaces-wrapper.sln`
  * or in visual studio code _Run and Debug_ view (`Ctrl + Shift + D`) the `.NET Core Launch (web) - Production`

## Usage

### TextGeneration

Once the application is running, the API can be used for sending the complete output: 

```pwsh
curl https://localhost:5001/models/phi-3-mini `
    -X POST `
    -d '{"inputs":"How to explain Internet for a medieval knight?","parameters":{"temperature":0.7}}' `
    -H 'Content-Type: application/json' `
    -k
```

or as streaming answer (word by word):

```pwsh
curl https://localhost:5001/models/phi-3-mini `
    -X POST `
    -d '{"inputs":"How to explain Internet for a medieval knight?","parameters":{"temperature":0.7}, "stream":true}' `
    -H 'Content-Type: application/json' `
    -k
```

### ChatCompletion

```pwsh
curl https://localhost:5001/v1/chat/completions `
      -X POST `
      -d '{"messages": [{"content": "How to explain Internet for a medieval knight?", "role": "user"}], "temperature":0.7, "stream":true}' `
      -H 'Content-Type: application/json' `
      -k
```

### Embeddings
```pwsh
curl https://localhost:5001/pipeline/feature-extraction/stable-diffusion `
     -X POST `
     -d '{"inputs": ["How to explain Internet for a medieval knight?"]}' `
     -H 'Content-Type: application/json' `
     -k
```

## Know Limitations

* Currently only runs under windows, as the NuGet packages used not yet supporting Linux
* The `modelName` (last part of the HTTP route) is ignored and it will always use the one model currently configured
* HuggingFace TextGenerationAPI defines property `new_max_tokens` that seems not be supported by PHI-3, the value is passed to the model as `max_length`
* HuggingFace TextGenerationAPI defined property `stop` that is used to configure stop words isn't possible to use, as GeneratorParams currently only support bool or double values
* TextGenerationResponse don't fill out details only the `generated_text`

# Design Decisions

* Focussing on CPU models, to avoid CPU hardware requirements
* Application should be able to run as container or kubernetes pod (used for development and testing)
* Mimic standard API (HuggingFace TextGenerationAPI) to integrate into rich ecosystem but still keep it simple


## License
This project is licensed under the terms of the MIT license.