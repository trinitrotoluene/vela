# Vela

## Never hand-write SpacetimeDB bindings

`src/Vela/Bindings/` is generated code. Don't edit, add or patch files there by hand, even to match
a schema change. Regenerate the whole directory with `spacetime generate` from the live module's
schema, as described in `README.md` ("Regenerating the SpacetimeDB bindings").
