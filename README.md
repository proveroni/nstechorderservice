# OrderService

API REST para gerenciamento de pedidos, desenvolvida como parte do desafio técnico da **nstech**.

A aplicação permite criar, consultar, confirmar e cancelar pedidos, incluindo validações de negócio, controle de estoque, autenticação JWT e consultas paginadas.

## Tecnologias utilizadas

- **C# / .NET 10** — ASP.NET Core Web API.
- **Entity Framework Core 10** — persistência e migrations.
- **PostgreSQL 17** — banco de dados relacional.
- **Docker e Docker Compose** — execução da API e do banco de dados.
- **JWT Bearer** — autenticação dos endpoints.
- **xUnit** — testes automatizados.
- **Testcontainers** — execução dos testes de integração com PostgreSQL isolado.
- **Bruno** — coleção de requisições HTTP e testes de API.

## Arquitetura

A solução utiliza os princípios de **Clean Architecture**, com separação de responsabilidades entre os projetos:

| Projeto | Responsabilidade |
|---|---|
| `OrderService.Domain` | Entidades e regras de negócio. |
| `OrderService.Application` | Casos de uso, contratos e orquestração das operações. |
| `OrderService.Infrastructure` | Persistência, autenticação e integrações de infraestrutura. |
| `OrderService.CrossCutting` | Componentes compartilhados entre as camadas. |
| `OrderService.Api` | Endpoints HTTP, configuração e inicialização da aplicação. |

As dependências são organizadas para manter as regras de negócio independentes dos detalhes de infraestrutura.

## Funcionalidades

- Criação de pedidos com múltiplos itens.
- Validação de produtos, quantidades e disponibilidade de estoque.
- Cálculo do valor total dos pedidos.
- Confirmação de pedidos com atualização do estoque.
- Cancelamento de pedidos com restituição do estoque quando aplicável.
- Operações de confirmação e cancelamento idempotentes.
- Consulta de pedidos por identificador.
- Listagem paginada com filtros por cliente, situação e período.
- Autenticação utilizando tokens JWT.
- Aplicação automática de migrations na inicialização.
- Testes unitários e de integração automatizados.

## Pré-requisitos

Para executar o projeto, são necessários:

- **.NET SDK 10** — para compilar e executar a aplicação localmente e executar os testes automatizados.
- **Docker Desktop ou Docker Engine** — para executar os contêineres da API e do PostgreSQL.
- **Docker Compose** — para inicializar os serviços da aplicação.
- **Bruno** (opcional) — para executar a coleção de requisições HTTP incluída no repositório.

O Docker deve estar em execução para utilizar o Docker Compose ou executar os testes de integração com Testcontainers.

### Verificação do ambiente

Na raiz da solução, execute:

```powershell
dotnet --version
```

```powershell
docker --version
```

```powershell
docker compose version
```

```powershell
docker info
```

O comando `docker info` deve conseguir se comunicar com o Docker Engine.

**Observação:** não é necessário instalar o PostgreSQL diretamente no sistema operacional. O banco de dados é executado em contêiner Docker.

Os testes de integração utilizam Testcontainers para criar um PostgreSQL temporário e isolado. Portanto, não é necessário iniciar os serviços com `docker compose up` antes de executar os testes.

## Configuração inicial

A aplicação utiliza arquivos de secrets para armazenar informações sensíveis fora do código-fonte.

Antes de iniciar os serviços, crie os arquivos necessários na pasta `docker/secrets`.

### 1. Criar a pasta de secrets

No PowerShell, a partir da raiz da solução:

```powershell
New-Item -ItemType Directory -Path .\docker\secrets -Force
```

Os arquivos dessa pasta não devem ser versionados no Git.

### 2. Configurar a senha do PostgreSQL

Crie o arquivo:

`docker/secrets/postgres_password`

Insira nele uma senha de sua escolha para o usuário `postgres`.

Exemplo ilustrativo:

```text
SUBSTITUA_POR_UMA_SENHA_SEGURA
```

### 3. Configurar a connection string

Crie o arquivo:

`docker/secrets/connection_string`

Insira uma connection string com os seguintes parâmetros:

```text
Host=postgres;Port=5432;Database=orders;Username=postgres;Password=SUBSTITUA_PELA_MESMA_SENHA
```

A senha deve ser idêntica à informada em `postgres_password`.

O hostname `postgres` corresponde ao nome do serviço do banco de dados no Docker Compose.

Na execução local em ambiente `Development`, a aplicação substitui automaticamente esse hostname por `localhost`, permitindo utilizar o mesmo arquivo de configuração.

**Importante:** os arquivos devem conter somente seus respectivos valores, sem aspas, nomes de variáveis ou espaços adicionais.

> [!NOTE]
> Senhas com caracteres especiais podem exigir tratamento específico
> na connection string do Npgsql.
>
> Recomenda-se utilizar uma senha aleatória, longa e alfanumérica.

### 4. Configurar a chave de assinatura JWT

A API utiliza JWT Bearer para autenticação. A chave de assinatura deve estar codificada em Base64 e representar pelo menos 32 bytes.

Para gerar uma chave criptograficamente segura, execute no PowerShell:

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Copie o resultado e salve no arquivo:

`docker/secrets/jwt_key`

O arquivo deve conter somente a chave gerada, sem aspas ou espaços adicionais.

> [!IMPORTANT]
> Utilize uma chave gerada especificamente para sua instalação. Não reutilize chaves de outros ambientes nem inclua seu valor no repositório.

> [!NOTE]
> A configuração `Jwt:SigningKey` é obrigatória. A aplicação valida o formato Base64 e exige pelo menos 32 bytes após a decodificação.

### 5. Configurar a senha do usuário de demonstração

A aplicação disponibiliza um usuário de demonstração para obtenção de tokens JWT.

Na configuração do Docker Compose, esse usuário possui:

| Configuração | Valor |
|---|---|
| Identificador | `1` |
| Usuário | `demo` |
| Senha | Definida durante a configuração inicial |

A senha não é armazenada em texto puro. A aplicação utiliza o mecanismo `PasswordHasher` do ASP.NET Core Identity para verificar o hash da senha.

Para gerar o hash, execute na raiz da solução:

```powershell
dotnet run --project .\tools\PasswordHashGenerator\PasswordHashGenerator.csproj
```

Quando solicitado, informe a senha desejada:

```text
Informe a senha do usuário de demonstração:
```

A ferramenta exibirá:

```text
Hash gerado:
<HASH_GERADO>
```

Copie somente o hash e salve no arquivo:

`docker/secrets/demo_user_password_hash`

> [!IMPORTANT]
> O arquivo deve conter o hash gerado, e não a senha em texto puro. Guarde a senha escolhida para autenticar-se no endpoint `POST /auth/token`.

> [!NOTE]
> A coleção Bruno contém uma requisição de autenticação com o usuário `demo`. Substitua a senha de exemplo pela senha escolhida durante esta configuração.

### 6. Conferir os arquivos de secrets

Ao concluir a configuração, a pasta `docker/secrets` deverá conter:

```text
docker/
└── secrets/
    ├── postgres_password
    ├── connection_string
    ├── jwt_key
    └── demo_user_password_hash
```

> [!WARNING]
> Não versione os arquivos dessa pasta nem compartilhe seus conteúdos. Eles armazenam credenciais e informações sensíveis necessárias à execução da aplicação.

## Executando a aplicação com Docker Compose

O Docker Compose inicializa dois serviços:

- **PostgreSQL 17:** banco de dados da aplicação.
- **OrderService API:** aplicação ASP.NET Core executada em ambiente `Production`.

A API aguarda o PostgreSQL passar pela verificação de saúde antes de iniciar.

### 1. Iniciar os serviços

Com o Docker Desktop ou Docker Engine em execução, abra um terminal na raiz da solução e execute:

```powershell
docker compose up --build -d
```

Esse comando compila a imagem da API, cria os contêineres necessários e inicia os serviços em segundo plano.

Na primeira execução, o Docker poderá precisar baixar as imagens do .NET e do PostgreSQL.

### 2. Verificar os serviços

Execute:

```powershell
docker compose ps
```

Os serviços `postgres` e `api` deverão aparecer em execução.

Para acompanhar os logs da API:

```powershell
docker compose logs -f api
```

Para acompanhar os logs do PostgreSQL:

```powershell
docker compose logs -f postgres
```

> [!NOTE]
> Na inicialização da API, as migrations do Entity Framework Core são aplicadas automaticamente ao banco de dados. Não é necessário executar `dotnet ef database update` manualmente.

### 3. Acessar a API

Após a inicialização, a API estará disponível em:

**http://localhost:8080**

Os endpoints de pedidos exigem autenticação JWT. Para obter um token, utilize `POST /auth/token` com as credenciais configuradas anteriormente.

> [!IMPORTANT]
> A API utiliza o ambiente `Production` dentro do Docker Compose. Portanto, não se deve presumir que a interface Swagger esteja habilitada nesse ambiente. A coleção Bruno pode ser utilizada para executar as requisições HTTP.

### 4. Encerrar os serviços

Para interromper e remover os contêineres:

```powershell
docker compose down
```

O volume do PostgreSQL é preservado, permitindo reutilizar os dados na próxima inicialização.

Para remover também o volume e os dados armazenados:

```powershell
docker compose down -v
```

> [!WARNING]
> O comando `docker compose down -v` remove o volume de dados do PostgreSQL. Utilize-o somente quando desejar descartar os dados persistidos nesse ambiente.

## Executando a aplicação localmente

Durante o desenvolvimento, é possível executar a API diretamente pelo .NET CLI ou pelo Visual Studio, mantendo apenas o PostgreSQL no Docker.

### 1. Iniciar o PostgreSQL

Na raiz da solução, execute:

```powershell
docker compose up -d postgres
```

Esse comando inicia somente o banco de dados, sem executar a API em contêiner.

Para verificar seu estado:

```powershell
docker compose ps postgres
```

Aguarde até que o serviço esteja saudável (`healthy`).

### 2. Executar a API pelo .NET CLI

Na raiz da solução, execute:

```powershell
dotnet run --project .\src\Api\OrderService.Api.csproj --launch-profile https
```

A API estará disponível nos endereços:

| Protocolo | Endereço |
|---|---|
| HTTPS | `https://localhost:7140` |
| HTTP | `http://localhost:5217` |

O perfil `https` configura automaticamente o ambiente `Development`.

> [!NOTE]
> Em ambiente `Development`, a API procura os arquivos em `docker/secrets` e adapta automaticamente o host da connection string para `localhost`.

> [!TIP]
> Caso o certificado HTTPS de desenvolvimento ainda não esteja configurado, execute `dotnet dev-certs https --trust` ou utilize o perfil `http`.

### 3. Executar pelo Visual Studio

1. Abra a solução `OrderService.slnx`.
2. Defina `OrderService.Api` como projeto de inicialização.
3. Selecione o perfil `https` ou `http`.
4. Pressione `F5` para iniciar com depuração ou `Ctrl + F5` para iniciar sem depuração.

A aplicação utilizará as mesmas configurações de desenvolvimento e os arquivos de secrets definidos anteriormente.

### 4. Inicialização do banco de dados

A aplicação aplica automaticamente as migrations do Entity Framework Core durante a inicialização.

> [!IMPORTANT]
> O PostgreSQL precisa estar disponível antes de iniciar a API. Não é necessário executar manualmente as migrations.

### 5. Encerrar o ambiente local

Interrompa a API pelo terminal ou pelo Visual Studio.

Para encerrar o PostgreSQL:

```powershell
docker compose stop postgres
```

Esse comando preserva o contêiner e o volume de dados para utilizações futuras.

## Autenticação JWT

A API utiliza autenticação **JWT Bearer** para proteger os endpoints de pedidos.

Para acessar esses endpoints, primeiro é necessário obter um token utilizando as credenciais do usuário de demonstração.

### 1. Obter um token

**Endpoint:** `POST /auth/token`

Exemplo utilizando a API local:

```http
POST https://localhost:7140/auth/token
Content-Type: application/json

{
  "username": "demo",
  "password": "SENHA_ESCOLHIDA"
}
```

Substitua `SENHA_ESCOLHIDA` pela senha informada durante a geração do hash na configuração inicial.

Uma autenticação bem-sucedida retorna um objeto contendo a propriedade `accessToken`.

### 2. Utilizar o token

Nas requisições aos endpoints protegidos, informe o token no cabeçalho HTTP:

```http
Authorization: Bearer SEU_ACCESS_TOKEN
```

### 3. Configuração e validade

A aplicação valida a assinatura, o emissor, o público e a expiração do token JWT.

| Configuração | Valor |
|---|---|
| Emissor (`Issuer`) | `OrderService` |
| Público (`Audience`) | `OrderService.Api` |
| Expiração em `Development` | 20 minutos |
| Expiração em `Production` via Docker Compose | 60 minutos |

> [!IMPORTANT]
> A chave de assinatura JWT deve ser mantida em segredo. Não inclua tokens ou chaves reais no código-fonte, na documentação ou em commits.

> [!NOTE]
> A coleção Bruno possui uma requisição **Auth / Authentication** que armazena automaticamente o `accessToken` recebido. As demais requisições utilizam esse token por meio da autenticação Bearer configurada na coleção.

## Endpoints da API

A API disponibiliza os seguintes endpoints:

| Método | Endpoint | Descrição | Autenticação |
|---|---|---|---|
| `POST` | `/auth/token` | Obtém um token JWT. | Não |
| `POST` | `/orders` | Cria um pedido. | JWT |
| `POST` | `/orders/{id}/confirm` | Confirma um pedido e atualiza o estoque. | JWT |
| `POST` | `/orders/{id}/cancel` | Cancela um pedido e restitui o estoque quando aplicável. | JWT |
| `GET` | `/orders/{id}` | Consulta um pedido e seus itens. | JWT |
| `GET` | `/orders` | Lista pedidos com filtros e paginação. | JWT |

### Criar um pedido

**Endpoint:** `POST /orders`

Exemplo de corpo da requisição:

```json
{
  "customerId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  "currency": "brl",
  "items": [
    {
      "productId": "11111111-1111-4111-8111-111111111111",
      "quantity": 2
    },
    {
      "productId": "22222222-2222-4222-8222-222222222222",
      "quantity": 1
    }
  ]
}
```

A operação valida os produtos, as quantidades e a disponibilidade de estoque, calcula o valor total e cria o pedido com situação inicial `Placed`.

Uma criação bem-sucedida retorna **HTTP 201 Created**, incluindo o identificador do pedido.

> [!NOTE]
> Os identificadores dos produtos utilizados no exemplo correspondem aos dados de demonstração utilizados na coleção Bruno. A criação depende da existência desses produtos e da disponibilidade de estoque.

### Consultar um pedido

**Endpoint:** `GET /orders/{id}`

Retorna os dados do pedido, incluindo identificador, cliente, moeda, situação, valor total, datas e itens.

### Confirmar um pedido

**Endpoint:** `POST /orders/{id}/confirm`

Confirma um pedido elegível e efetua a atualização correspondente no estoque.

A operação é idempotente e, quando bem-sucedida, retorna **HTTP 204 No Content**.

### Cancelar um pedido

**Endpoint:** `POST /orders/{id}/cancel`

Cancela um pedido elegível. Quando o pedido já foi confirmado, o estoque reservado é restituído conforme as regras de negócio.

A operação é idempotente e, quando bem-sucedida, retorna **HTTP 204 No Content**.

### Listar pedidos

**Endpoint:** `GET /orders`

A listagem aceita os seguintes parâmetros opcionais:

| Parâmetro | Descrição |
|---|---|
| `customerId` | Filtra pelo identificador do cliente. |
| `status` | Filtra pela situação do pedido. |
| `from` | Data inicial do período de criação. |
| `to` | Data final do período de criação. |
| `page` | Número da página, iniciado em 1. |
| `pageSize` | Quantidade de registros por página, entre 1 e 100. |

Exemplo:

```http
GET /orders?page=1&pageSize=10&status=2
Authorization: Bearer SEU_ACCESS_TOKEN
```

A resposta contém as propriedades `items`, `page`, `pageSize`, `totalCount` e `totalPages`.

### Situações dos pedidos

| Valor | Situação |
|---|---|
| `1` | `Placed` — pedido criado. |
| `2` | `Confirmed` — pedido confirmado. |
| `3` | `Canceled` — pedido cancelado. |

> [!TIP]
> A coleção Bruno, localizada em `bruno/OrderService`, contém exemplos de requisições e testes automatizados para os endpoints. Ela permite executar o fluxo de autenticação, criação, consulta, confirmação e cancelamento de pedidos.

## Testes automatizados

A solução possui testes unitários e de integração utilizando **xUnit**.

Os testes verificam regras de negócio, validações, casos de uso e operações de persistência.

### Organização dos projetos de testes

| Projeto | Responsabilidade |
|---|---|
| `OrderService.Domain.UnitTests` | Testes das entidades e regras de domínio. |
| `OrderService.Application.UnitTests` | Testes dos casos de uso e validações da aplicação. |
| `OrderService.CrossCutting.UnitTests` | Testes dos componentes compartilhados. |
| `OrderService.Infrastructure.IntegrationTests` | Testes de integração com PostgreSQL real. |

### Executar todos os testes

Na raiz da solução, execute:

```powershell
dotnet test .\OrderService.slnx
```

O comando executa os testes automatizados incluídos na solução.

### Executar somente os testes de integração

```powershell
dotnet test .\tests\Integration\Infrastructure\OrderService.Infrastructure.IntegrationTests.csproj
```

### Testes de integração com Testcontainers

Os testes de integração utilizam **Testcontainers for .NET** para executar uma instância isolada do PostgreSQL 17.

Durante a execução:

1. O Testcontainers cria e inicia um contêiner temporário do PostgreSQL.
2. A aplicação de testes obtém dinamicamente a connection string do contêiner.
3. As migrations do Entity Framework Core são aplicadas ao banco temporário.
4. Os testes de integração são executados.
5. Ao finalizar, os recursos do contêiner são descartados.

A configuração do banco de testes está localizada em:

`tests/Integration/Infrastructure/appsettings.Testing.json`

> [!IMPORTANT]
> O Docker Desktop ou Docker Engine deve estar em execução para que os testes de integração funcionem.

> [!NOTE]
> Não é necessário executar `docker compose up` antes dos testes. O Testcontainers gerencia seu próprio PostgreSQL, separado do banco utilizado pela aplicação.

> [!TIP]
> Os testes unitários não dependem do PostgreSQL. Caso o Docker não esteja disponível, é possível executar individualmente os projetos de testes unitários.

### Validação

Na última execução completa realizada durante o desenvolvimento, a solução apresentou:

- **184 testes automatizados aprovados.**
- **42 testes de integração aprovados.**

Esses números representam o resultado da validação realizada durante o desenvolvimento e podem mudar conforme a evolução do projeto.

## Decisões arquiteturais e técnicas

### Clean Architecture

A solução foi organizada em camadas, seguindo os princípios de Clean Architecture e SOLID.

Essa separação permite manter as regras de negócio independentes dos mecanismos de persistência, autenticação e exposição HTTP.

As responsabilidades estão distribuídas entre:

- **Domain:** entidades e regras de negócio.
- **Application:** casos de uso, contratos e orquestração.
- **Infrastructure:** implementação da persistência e dos serviços de infraestrutura.
- **Api:** endpoints HTTP e configuração da aplicação.
- **CrossCutting:** componentes compartilhados.

### Persistência com Entity Framework Core

Foi utilizado Entity Framework Core 10 com PostgreSQL 17.

As operações de acesso a dados são implementadas na camada Infrastructure, mantendo os casos de uso desacoplados dos detalhes do banco de dados.

As migrations são aplicadas automaticamente durante a inicialização da API, simplificando a preparação do ambiente de avaliação.

### Consistência do estoque e controle de concorrência

O controle de estoque utiliza transações explícitas do Entity Framework Core, atualizações condicionais e bloqueio de linhas do PostgreSQL.

A criação de um pedido não reduz imediatamente o estoque. A redução acontece durante sua confirmação.

Para garantir a consistência das operações:

- **Atualização atômica:** o estoque é reduzido por meio de `ExecuteUpdateAsync`, com uma condição que exige quantidade disponível suficiente.
- **Proteção contra estoque negativo:** quando a condição não é atendida, nenhuma linha é atualizada e a confirmação retorna falha. O banco também possui uma restrição `CHECK` que impede valores negativos em `stock_quantity`.
- **Bloqueio por pedido:** as operações de confirmação e cancelamento utilizam `SELECT ... FOR UPDATE`, evitando que duas transações modifiquem simultaneamente o estado do mesmo pedido.
- **Transações:** alterações de estoque e de situação do pedido são executadas na mesma transação, com `COMMIT` em caso de sucesso e `ROLLBACK` em caso de falha.
- **Cancelamento:** pedidos confirmados têm suas quantidades devolvidas ao estoque. Pedidos ainda não confirmados podem ser cancelados sem movimentação de estoque.
- **Idempotência:** confirmar um pedido já confirmado ou cancelar um pedido já cancelado não repete a movimentação do estoque.

Essa combinação protege a consistência das operações mesmo quando diferentes requisições são processadas concorrentemente.

> [!NOTE]
> O controle de concorrência utiliza atualizações condicionais e bloqueios transacionais do PostgreSQL, sem depender de uma propriedade `RowVersion` nas entidades.


### Idempotência das operações

As operações de confirmação e cancelamento foram concebidas para serem idempotentes.

Isso permite lidar com repetições de requisições sem aplicar novamente os efeitos de uma operação já concluída.

A idempotência é especialmente importante em APIs sujeitas a novas tentativas por clientes HTTP ou problemas de comunicação.

### Autenticação e segurança

A API utiliza autenticação JWT Bearer, com validação de assinatura, emissor, público e expiração.

As credenciais e a chave de assinatura são fornecidas por arquivos de secrets ou variáveis de ambiente, evitando seu armazenamento diretamente no código-fonte.

Na execução com Docker, a aplicação utiliza uma imagem de runtime separada da imagem de compilação e é executada com usuário sem privilégios.

> [!NOTE]
> O usuário de demonstração foi implementado para permitir a avaliação da API. Em um ambiente produtivo, a autenticação poderia ser integrada a um provedor de identidade dedicado.

### Testes de integração isolados

A solução utiliza Testcontainers para disponibilizar uma instância temporária do PostgreSQL durante os testes de integração.

Essa abordagem permite validar o comportamento da persistência com um banco real, sem depender do banco utilizado na execução normal da aplicação.

O isolamento também reduz a interferência entre o ambiente de desenvolvimento e a execução dos testes.

### Operações assíncronas

Os casos de uso e as operações de persistência utilizam `async/await` para executar operações de entrada e saída sem bloquear desnecessariamente as threads da aplicação.

Essa abordagem favorece a utilização eficiente dos recursos durante o processamento de requisições concorrentes.

### Documentação e testes HTTP

A coleção Bruno disponibiliza exemplos de requisições e verificações automatizadas das respostas HTTP.

As convenções de documentação Markdown estão registradas em `docs/CONTRIBUTING.md`, incluindo a utilização de GitHub Alerts para observações e avisos.

## Testando a API com Bruno

O projeto inclui uma coleção de requisições HTTP para o [Bruno](https://www.usebruno.com/), permitindo explorar e testar os endpoints da API.

A coleção está disponível em:

`bruno/OrderService`

### Abrir a coleção

1. Inicie a API utilizando Docker Compose ou .NET CLI.
2. Abra o Bruno.
3. Selecione **Open Collection**.
4. Escolha a pasta `bruno/OrderService`.
5. Selecione o ambiente **Local**.

### Configurar o ambiente

O ambiente `Local` utiliza a variável `baseUrl`, definida em:

`bruno/OrderService/environments/Local.yml`

O endereço padrão é:

```text
https://localhost:7140
```

Esse endereço corresponde à execução local da API utilizando o perfil HTTPS.

Para testar a aplicação executada pelo Docker Compose, altere a variável `baseUrl` para:

```text
http://localhost:8080
```

### Autenticação

Execute primeiro a requisição de autenticação:

`POST /auth/token`

Informe o usuário `demo` e a senha configurada durante a preparação do ambiente.

Após uma autenticação bem-sucedida, a coleção armazena automaticamente o token JWT na variável `accessToken`.

As demais requisições utilizam esse token na autenticação Bearer.

> [!IMPORTANT]
> Substitua a senha de exemplo da requisição de autenticação pela senha escolhida durante a configuração inicial.

### Fluxo de testes

A coleção permite executar o fluxo principal da aplicação:

1. Autenticar o usuário.
2. Criar um pedido.
3. Consultar o pedido criado.
4. Confirmar o pedido.
5. Cancelar o pedido.
6. Listar os pedidos utilizando filtros e paginação.

A requisição de criação armazena automaticamente o identificador retornado na variável `orderId`, permitindo reutilizá-lo nas requisições seguintes.

A coleção também inclui verificações automatizadas das respostas HTTP, incluindo testes da estrutura de paginação na listagem de pedidos.

> [!NOTE]
> Para utilizar o ambiente HTTPS local, o certificado de desenvolvimento do ASP.NET Core deve estar configurado e ser confiável no sistema.
