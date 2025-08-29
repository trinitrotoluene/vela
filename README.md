# Vela

![Deploy Status](https://img.shields.io/github/actions/workflow/status/trinitrotoluene/vela/deploy.yml?branch=master&style=flat-square)

This project is an event gateway wrapping the SpacetimeDB bindings to bitcraft. It maintains a redis cache and uses pub/sub to notify subscribers of cache changes.

## Exporting schema

```sh
dotnet run --project src/Vela.Gen -- generate-json-schema --outDir ./schema-output
```
