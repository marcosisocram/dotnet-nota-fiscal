# Fiscal Documents API - Desafio Técnico Senior .NET

API desenvolvida para o processamento, armazenamento e gestão de documentos fiscais eletrônicos (NFe, CTe e NFSe).

## 🚀 Tecnologias Utilizadas

- **.NET 10 (ASP.NET Core)**: Minimal APIs para performance e baixo overhead.
- **PostgreSQL**: Banco de dados relacional para garantir integridade e consistência fiscal.
- **RabbitMQ**: Message Broker para processamento assíncrono.
- **MassTransit**: Abstração de Service Bus para mensageria resiliente.
- **Entity Framework Core**: ORM para persistência de dados.
- **NUnit & FluentAssertions**: Testes unitários e de integração.
- **k6**: Testes de carga e performance.
- **Docker & Docker Compose**: Orquestração da infraestrutura local.

## 🏛️ Arquitetura e Decisões Técnicas

### Modelagem de Dados: SQL (PostgreSQL)
Optei pelo uso de **SQL (PostgreSQL)** em vez de MongoDB pelas seguintes razões:
1. **Integridade Referencial**: Documentos fiscais exigem forte relação entre cabeçalho e itens (`det`), garantindo que itens nunca fiquem órfãos.
2. **Idempotência Nativa**: O uso de Constraints `UNIQUE` na Chave de Acesso (`AccessKey`) garante, a nível de banco de dados, que o mesmo documento nunca será inserido em duplicidade, mesmo sob alta concorrência.
3. **Consistência ACID**: Crucial para sistemas financeiros/fiscais onde a perda de dados ou inconsistências não são aceitáveis.

### Mensageria e Resiliência
Utilizei o **MassTransit** com RabbitMQ para:
- **Desacoplamento**: A API notifica o processamento e segue livre, enquanto consumidores (como o de geração de resumo) processam em background.
- **Retry Policy**: Implementado automaticamente pelo MassTransit para lidar com falhas temporárias (ex: banco ocupado).
- **Separação de Eventos**: Criados eventos distintos para `Processado` e `Atualizado`.

### Tratamento de Dados Sensíveis
Seguindo as boas práticas da LGPD e segurança da informação:
- **Ofuscamento em Logs**: No `FiscalDocumentProcessedConsumer`, o CNPJ do emitente é ofuscado (ex: `07.***.***/0001-89`) antes de ser registrado nos logs, protegendo a informação sensível sem perder a capacidade de rastreio.
- **Normalização de Dados**: Dados do XML são sanitizados via Regex e normalizados para UTC antes da persistência.

## 🛠️ Como Executar

### 1. Infraestrutura (Docker)
Para subir o banco de dados e o RabbitMQ:
```bash
docker compose up -d
```

### 2. Aplicação
```bash
dotnet run --project SIEGWebApplication
```
A API estará disponível em `http://localhost:5266` e o Swagger em `http://localhost:5266/openapi/v1.json` (ou via arquivo `swagger.yaml` na raiz).

### 3. Testes Automatizados
```bash
# Unitários e Integração
dotnet test SIEGWebApplication.Tests
```

### 4. Carga (k6)
```bash
k6 run load-tests/basic-load-test.js
k6 run load-tests/idempotency-test.js
k6 run load-tests/mixed-workload-test.js
```

## 📡 Endpoints (v1)

A API segue o prefixo `/api/v1/fiscal-documents`:
- `GET /`: Listagem paginada com filtros (`cnpj`, `uf`, `startDate`, `endDate`).
- `GET /{id}`: Detalhes do documento e itens.
- `GET /{id}/xml`: Retorna apenas o XML original.
- `POST /xml`: Criação via Raw Body (XML).
- `POST /upload`: Criação via Upload de arquivo.
- `PATCH /{id}`: Atualização parcial de metadados.
- `PUT /upload`: Atualização total via novo arquivo XML.
- `DELETE /{id}`: Remoção do documento.

## 🔮 Melhorias Futuras (Backlog)

Se houvesse mais tempo, as seguintes implementações seriam priorizadas:
1. **Cache com Redis**: Implementar cache distribuído nos endpoints de detalhes para reduzir a carga no PostgreSQL.
2. **Observabilidade**: Integrar OpenTelemetry com Jaeger e Grafana para rastreabilidade ponta-a-ponta entre API e Consumidores.
3. **Autenticação**: Adicionar OAuth2/JWT com escopos por tipo de operação (ex: leitura vs escrita).
4. **Schema Validation**: Implementar validação de XSD para garantir que o XML recebido segue rigorosamente os padrões da SEFAZ antes mesmo do parsing.
5. **Ofuscamento**: Identificar mais campos que precisam serem ofuscados na Nota Fiscal
