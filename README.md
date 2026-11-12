<div align="center">
  &nbsp;&nbsp;&nbsp;&nbsp;
  <img src="https://cdn.jsdelivr.net/gh/devicons/devicon@latest/icons/rabbitmq/rabbitmq-original.svg" alt="RabbitMQ Logo" width="90" />
  <h1>OrderFlow Engine</h1>
  <p><strong>Resilient Event-Driven Architecture with .NET 8, MassTransit, RabbitMQ & Redis</strong></p>
</div>

---

## Arquitetura & Topologia AMQP

```text
[Client] ──(HTTP POST)──▶ [OrderFlow.Api (Vertical Slice)]
                                 │
                                 ▼ Publish (Persistent + CorrelationId + EventId)
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
               ┌─────────────────┴─────────────────┐
               ▼                                   ▼
     [OrderFlow.Worker #1]               [OrderFlow.Worker #2]
               │                                   │
               └─────────────────┬─────────────────┘
                                 ▼
               [Pipeline Filter: IdempotencyConsumeFilter] ◀──▶ [Redis (SET NX EX)]
                                 │
                  ┌──────────────┴──────────────┐
                  │                             │
             [Sucesso]               [Falha após 3 Retries]
                  │                             │
                  ▼                             ▼
              BasicAck             [Queue: order-flow.order-placed_error]
                                   (Dead Letter Queue / Auditoria)

```

### Pilares Arquiteturais Implementados

- **Vertical Slice & Clean Composition:** Endpoints isolados por feature (`Features/Orders`) e configurações de infraestrutura encapsuladas em métodos de extensão (`Extensions/`), mantendo o `Program.cs` declarativo.
- **Topologia Explícita & Durabilidade:** Exchanges e filas duráveis (`durable = true`) com mensagens persistentes (`delivery_mode = 2`), sobrevivendo a reinicializações do broker.
- **Idempotência Distribuída via Pipeline Filter:** Middleware global no MassTransit (`IdempotencyConsumeFilter<T>`) utilizando operações atômicas no **Redis** (`SET NX EX`), permitindo escalar múltiplas réplicas de workers sem processamento duplicado.
- **Resiliência & Backoff Incremental:** Política de retentativas automáticas (3 tentativas adicionais com intervalos crescentes de `1s`, `3s` e `5s`).
- **Dead Letter Queue (DLQ):** Isolamento automático de mensagens com falha persistente na fila `order-flow.order-placed_error`, preservando stack traces e headers originais sem bloquear o consumo principal.
- **Controle de Vazão (QoS):** `PrefetchCount = 16` limitando mensagens não confirmadas em trânsito por consumidor.

---

## Passo a Passo de Execução e Validação

### Passo 1: Subir o Cluster Completo (com 2 Réplicas do Worker)

Na raiz do repositório, execute:

```bash
docker compose up --build -d --scale worker=2

```

Verifique se todos os serviços (`rabbitmq`, `redis`, `api` e as 2 instâncias de `worker`) estão saudáveis:

```bash
docker compose ps

```

### Passo 2: Validar Saúde e Interfaces Web

1. **Health Check da API + Broker:**

```bash
curl -s http://localhost:5050/health

```

2. **Interfaces de Gerenciamento:**

- **Swagger UI (API):** [http://localhost:5050/swagger](http://localhost:5050/swagger)
- **RabbitMQ Management UI:** [http://localhost:15672](http://localhost:15672) (Usuário: `guest` | Senha: `guest`)

### Passo 3: Testar o Fluxo Normal (Happy Path)

Em uma aba do terminal, acompanhe os logs distribuídos entre as duas instâncias de workers:

```bash
docker compose logs -f worker

```

Em outra aba, envie um pedido válido com itens:

```bash
curl -X POST http://localhost:5050/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "cliente@empresa.com",
    "currency": "BRL",
    "items": [
      { "sku": "LEGO-42096", "productName": "Porsche 911 RSR", "quantity": 1, "unitPrice": 1499.90 },
      { "sku": "TECH-001", "productName": "Cabo USB-C", "quantity": 2, "unitPrice": 45.00 }
    ],
    "simulateDownstreamFailure": false
  }'

```

### Passo 4: Testar Resiliência, Retries e Dead Letter Queue (DLQ)

Dispare um pedido simulando indisponibilidade de gateway externo (`simulateDownstreamFailure: true`):

```bash
curl -X POST http://localhost:5050/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "auditoria@empresa.com",
    "currency": "BRL",
    "items": [
      { "sku": "SRV-999", "productName": "Licença Enterprise", "quantity": 1, "unitPrice": 2500.00 }
    ],
    "simulateDownstreamFailure": true
  }'

```

1. Nos logs do `worker`, observe o filtro de idempotência liberando o lock em caso de exceção para permitir que o motor de retry execute a tentativa inicial + 3 retries (`1s`, `3s`, `5s`).
2. Esgotadas as tentativas, inspecione a mensagem isolada na fila `order-flow.order-placed_error` em [http://localhost:15672/#/queues](http://localhost:15672/#/queues).

### Passo 5: Encerrar o Ambiente

```bash
docker compose down -v

```

---

## Trade-offs & Próximos Passos (Evolução para Produção)

- **Transactional Outbox / Inbox:** Atualmente a API publica diretamente no broker e o Worker controla idempotência via Redis com liberação de lock em exceções. Em cenários financeiros críticos com persistência relacional, recomenda-se o _MassTransit Entity Framework Outbox_ para garantir atomicidade ACID entre banco de dados e mensageria.
- **Testes de Integração:** Adicionar suíte com `xUnit` + `Testcontainers` para validar topologia, retries e DLQ em pipeline de CI/CD.
