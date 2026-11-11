<div align="center">
  <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/rabbitmq/rabbitmq-original.svg" alt="RabbitMQ Logo" width="100" />
  <h1>OrderFlow Engine</h1>
  <p><strong>Resilient Event-Driven Architecture with .NET 8, MassTransit & RabbitMQ</strong></p>
</div>

Arquitetura orientada a eventos (EDA) construída em **.NET 8** com **MassTransit** e **RabbitMQ**, demonstrando padrões corporativos de resiliência, tolerância a falhas, processamento idempotente e observabilidade distribuída.

---

## Arquitetura & Topologia AMQP

```text
[Client] ──(HTTP POST)──▶ [OrderFlow.Api (Producer)]
                                 │
                                 ▼ Publish (Persistent + CorrelationId)
                 ┌───────────────────────────────┐
                 │ Exchange: OrderPlacedEvent    │
                 │ (Fanout, Durable)             │
                 └───────────────┬───────────────┘
                                 │ Binding
                                 ▼
                 ┌───────────────────────────────┐
                 │ Queue: order-flow.order-placed│ ◄── PrefetchCount: 16 (QoS)
                 │ (Durable)                     │
                 └───────────────┬───────────────┘
                                 │
                      [OrderFlow.Worker]
                                 │
                  ┌──────────────┴──────────────┐
                  │                             │
             [Sucesso]               [Falha após 3 Retries]
                  │                             │
                  ▼                             ▼
           Idempotency Store       [Queue: order-flow.order-placed_error]
           + BasicAck              (Dead Letter Queue / Auditoria)
```

## Pilares Implementados

- **Topologia Explícita & Durabilidade:** Exchanges e filas duráveis (`durable = true`) com mensagens persistentes (`delivery_mode = 2`), sobrevivendo a reinicializações do broker.
- **Resiliência & Backoff Incremental:** Política de retentativas automáticas (3 tentativas com intervalos crescentes de 1s, 3s e 5s) para falhas transitórias a jusante.
- **Dead Letter Queue (DLQ):** Isolamento automático de mensagens com falha persistente na fila `order-flow.order-placed_error`, preservando stack traces e headers originais sem bloquear o consumo principal.
- **Idempotência:** Deduplicação de mensagens baseada em `CorrelationId` antes da execução de efeitos colaterais de negócio.
- **Controle de Vazão (QoS):** `PrefetchCount = 16`, limitando mensagens não confirmadas em trânsito por consumidor.
- **Observabilidade & Health Checks:** Propagação de `CorrelationId` nos logs estruturados e endpoint `/health` validando a conectividade ativa com o broker.

---

## Quickstart (Execução em 1 Comando)

Suba o cluster completo (RabbitMQ + API + Worker) via Docker Compose:

bash

```bash
docker compose up --build -d
```

**Swagger UI (API):** [http://localhost:5050/swagger](http://localhost:5050/swagger)

**Health Check:** [http://localhost:5050/health](http://localhost:5050/health)

**RabbitMQ Management UI:** [http://localhost:15672](http://localhost:15672) (`guest / guest`)

---

## Cenários de Teste

### 1\. Fluxo Normal (Happy Path)

Execute a requisição para simular o processamento normal de um pedido:

bash

```bash
curl -X POST http://localhost:5050/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "cliente@empresa.com",
    "totalAmount": 320.50,
    "simulateDownstreamFailure": false
  }'
```

Verifique o processamento nos logs do Worker:

bash

```bash
docker compose logs -f worker
```

### 2\. Tolerância a Falhas & Roteamento para DLQ

Execute a requisição simulando uma falha no serviço downstream:

bash

```bash
curl -X POST http://localhost:5050/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "auditoria@empresa.com",
    "totalAmount": 999.90,
    "simulateDownstreamFailure": true
  }'
```

Acompanhe as 3 retentativas com backoff nos logs do Worker e inspecione a mensagem capturada na fila `order-flow.order-placed_error` através do painel web do RabbitMQ:

[http://localhost:15672/#/queues](http://localhost:15672/#/queues)

---

## Encerramento

Para interromper os containers e remover os volumes associados:

bash

```bash
docker compose down -v
```
