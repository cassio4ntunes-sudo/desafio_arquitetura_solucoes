# Contrato consumido — cópia local

Este projeto **não é** o domínio completo do Fluxo de Caixa. É a cópia local do
**contrato publicado** pelo serviço de lançamentos: o evento `LancamentoRegistrado`.

Em multi-repo, o consumidor não compartilha código com o produtor — ele copia o
contrato (ou o consome como pacote). Por isso aqui existe apenas o evento, e não
agregados, value objects ou modelos de leitura: o worker nunca precisou deles.

## Regra de evolução

O contrato é **Published Language** (ver `docs/DOMINIOS-E-CAPACIDADES.md`).
Mudança incompatível aqui quebra a consolidação em produção, porque produtor e
consumidor são implantados em momentos diferentes. Portanto:

- **Permitido:** adicionar campo opcional
- **Proibido:** remover campo, renomear campo ou trocar tipo sem versionar a mensagem

A alternativa a esta cópia é um pacote `FluxoDeCaixa.Contracts` publicado em feed
interno — o trade-off está registrado no ADR-18.
