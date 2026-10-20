# Decisões Técnicas — OrderService

Este documento registra as principais decisões arquiteturais e de domínio do OrderService, suas justificativas, alternativas consideradas e consequências.

As decisões serão atualizadas durante o desenvolvimento. A implementação e os testes deverão confirmar os comportamentos documentados.

## ADR-001 — Utilização do .NET 10

**Status:** Aceita

**Contexto:** O teste técnico exige .NET 8 ou superior.

**Decisão:** Utilizar .NET 10 LTS e C# 14.

**Justificativa:** O .NET 10 atende ao requisito e possui ciclo de suporte de longo prazo. O ambiente de desenvolvimento já dispõe do SDK .NET 10.

**Consequências:** O ambiente de execução e as imagens Docker deverão ser compatíveis com .NET 10.

**Referência:** https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

## ADR-002 — Separação em quatro camadas

**Status:** Aceita

**Contexto:** O teste exige Clean Architecture, SOLID e separação clara entre Domain, Application, Infrastructure e API.

**Decisão:** Organizar a solução em quatro projetos principais, com dependências direcionadas para as camadas internas.

**Justificativa:** Isolar regras de negócio de detalhes de infraestrutura e transporte, permitindo testes de domínio independentes de banco de dados e HTTP.

**Alternativas consideradas:** Arquitetura em projeto único; criação de camadas e abstrações adicionais.

**Consequências:** Maior separação de responsabilidades, com custo de organização e configuração entre projetos. Abstrações serão introduzidas apenas quando tiverem uma responsabilidade concreta.

**Referência:** https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html

## ADR-003 — Baixa de estoque na confirmação

**Status:** Aceita

**Contexto:** O serviço deve criar, confirmar e cancelar pedidos, validando estoque e garantindo idempotência.

**Decisão:**
- Na criação, validar existência dos produtos e disponibilidade de estoque, sem realizar baixa.
- Na confirmação, verificar novamente a disponibilidade e baixar o estoque.
- No cancelamento de pedido Placed, não alterar o estoque.
- No cancelamento de pedido Confirmed, repor as quantidades baixadas.
- Repetições de confirmação ou cancelamento já concluídos não produzirão novos efeitos sobre o estoque.
- Não permitir confirmação de pedidos cancelados.

**Justificativa:** Essa abordagem atende ao ciclo de vida exigido sem introduzir um subsistema adicional de reservas e expiração.

**Alternativa considerada:** Reservar estoque durante a criação do pedido.

**Consequências e limitações:**
- Um pedido Placed não garante disponibilidade futura.
- A confirmação pode falhar por insuficiência de estoque.
- Confirmação e cancelamento exigem transações consistentes.
- A concorrência é controlada na persistência por meio de transações, atualizações condicionais de estoque e bloqueio da linha do pedido, conforme a ADR-005.
- A idempotência é preservada nas operações de confirmação e cancelamento, inclusive diante de requisições concorrentes.

**Referências:**
- https://www.postgresql.org/docs/current/transaction-iso.html
- https://learn.microsoft.com/en-us/ef/core/saving/transactions
- https://learn.microsoft.com/en-us/ef/core/saving/concurrency

## ADR-004 — Preço registrado no item do pedido

**Status:** Aceita

**Contexto:** O pedido deve calcular seu total a partir dos preços dos produtos e das quantidades solicitadas.

**Decisão:** Obter o preço do cadastro de produtos durante a criação e registrar o valor unitário no OrderItem.

**Justificativa:** Preservar o valor comercial utilizado na criação do pedido, independentemente de alterações posteriores no cadastro do produto.

**Consequências:**
- O cliente não define o preço por meio do payload.
- O total será calculado a partir dos preços registrados nos itens.
- A moeda do pedido e a compatibilidade com a moeda dos produtos seguem a ADR-019.
- As regras de precisão monetária devem permanecer coerentes entre domínio e persistência.

**Natureza da decisão:** Regra de modelagem adotada para este serviço, não exigência explícita de armazenamento histórico no enunciado.

## ADR-005 — Controle transacional de estoque

**Status:** Aceita e implementada

**Contexto:** Confirmações simultâneas não podem consumir mais estoque do que o disponível. Da mesma forma, confirmações e cancelamentos concorrentes de um mesmo pedido não podem produzir movimentações duplicadas.

**Decisão:**

- Utilizar atualizações condicionais de estoque no PostgreSQL, por meio do `ExecuteUpdateAsync()` do Entity Framework Core.
- Na confirmação, descontar o estoque somente quando a quantidade disponível for suficiente.
- No cancelamento de pedidos confirmados, repor o estoque por meio de atualização condicional, respeitando os limites do tipo numérico.
- Utilizar `SELECT ... FOR UPDATE` para bloquear a linha do pedido durante as operações de confirmação e cancelamento.
- Executar a movimentação de estoque e a alteração do estado do pedido dentro de uma mesma transação explícita.
- Confirmar a transação somente após a conclusão de todas as operações.
- Realizar rollback quando qualquer operação falhar, impedindo alterações parciais.
- Preservar a idempotência das operações, evitando descontos ou reposições repetidos.

**Justificativa:** As atualizações condicionais impedem que a disponibilidade de estoque seja validada apenas com base em uma leitura anterior, potencialmente desatualizada. O bloqueio pessimista da linha do pedido serializa as alterações concorrentes sobre o mesmo pedido. A transação garante que a movimentação de estoque e a mudança de estado sejam persistidas atomicamente.

**Alternativas consideradas:**

- Controle de concorrência otimista utilizando token de versão.
- Bloqueio pessimista de todas as linhas de produtos antes da movimentação.
- Reserva de estoque durante a criação do pedido.

**Consequências:**

- A confirmação não permite que o estoque fique negativo.
- Uma falha na movimentação de qualquer item provoca rollback da transação.
- Confirmações e cancelamentos concorrentes do mesmo pedido são coordenados pelo bloqueio da linha correspondente.
- Atualizações concorrentes de estoque são protegidas pelas condições executadas diretamente no banco de dados.
- As operações dependem dos recursos transacionais e de bloqueio do PostgreSQL.
- Deadlocks e falhas transitórias continuam sendo possibilidades operacionais que precisam ser tratadas adequadamente.
- A implementação foi exercitada por testes automatizados, incluindo testes de integração com PostgreSQL.

**Referências:**

- https://www.postgresql.org/docs/current/transaction-iso.html
- https://www.postgresql.org/docs/current/explicit-locking.html
- https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- https://learn.microsoft.com/en-us/ef/core/saving/transactions
- https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete

## ADR-006 — Identificadores das entidades

**Status:** Aceita

**Contexto:** O enunciado não determina o tipo dos
identificadores das entidades.

**Decisão:** Utilizar Guid para identificar pedidos e produtos.

**Justificativa:** Guid possui suporte nativo no .NET e
mapeamento para o tipo uuid do PostgreSQL por meio do Npgsql,
sem necessidade de conversores personalizados.

**Alternativa considerada:** ULID.

**Consequências:**
- Os identificadores serão representados por Guid no domínio.
- O PostgreSQL utilizará colunas uuid.
- A estratégia de geração dos identificadores será definida
  durante a implementação das entidades.
- A escolha não pressupõe que Guid ofereça melhor desempenho
  de indexação que outras estratégias de identificação.

**Referência:**
https://www.npgsql.org/efcore/mapping/general.html


## ADR-007 — Result Pattern sem herança

**Status:** Aceita

**Contexto:** O domínio e a aplicação precisam representar
falhas esperadas sem utilizar exceções como mecanismo
habitual de controle de fluxo.

**Decisão:** Implementar Result e Result<T> como classes
independentes no projeto OrderService.CrossCutting.

Ambas terão métodos estáticos Success e Failure,
propriedades IsSuccess, IsFailure e Errors.

Result<T> também disponibilizará Value, acessível
somente quando a operação for bem-sucedida.

**Justificativa:** A independência elimina a necessidade
de ocultação de métodos estáticos herdados e simplifica
os contratos públicos.

**Alternativa considerada:** Herança de Result<T>
a partir de Result.

**Consequências:**
- Pequena duplicação de validações internas.
- Ausência de acoplamento por herança entre os resultados.
- Falhas esperadas serão representadas por erros estruturados.
- Violações dos contratos internos poderão lançar exceções.
- Exceções inesperadas serão tratadas centralmente na API.
- A API utilizará ProblemDetails para respostas HTTP de erro,
  com mapeamento apropriado entre falhas de negócio e
  códigos de status.

**Referências:**
https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions

https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling

https://www.rfc-editor.org/rfc/rfc9457.html


## ADR-008 — Validação de OrderItem por Result Pattern

**Status:** Aceita

**Contexto:** A criação de itens de pedido exige validações
de identificador, quantidade, preço e total calculado.

**Decisão:** Utilizar um construtor privado e um método
estático Create que retorna Result<OrderItem>.

O domínio referencia OrderService.CrossCutting para utilizar
os contratos compartilhados Error e Result<T>.

**Justificativa:** Falhas esperadas de validação são
representadas explicitamente, sem utilizar exceções
como mecanismo habitual de controle de fluxo.

**Consequências:**
- A criação de itens passa obrigatoriamente pelas validações.
- Múltiplos erros podem ser retornados em uma operação.
- O domínio possui dependência de um projeto compartilhado.
- O preço unitário registrado permanece associado ao item.
- A existência do produto e a disponibilidade de estoque
  serão verificadas em etapas posteriores.

**Referência:**
https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions


## ADR-009 — Estados suportados pelo pedido

**Status:** Aceita

**Contexto:** O serviço de pedidos não possui operações
para criação ou manutenção de rascunhos.

**Decisão:** Utilizar somente os estados Placed,
Confirmed e Canceled no OrderStatusEnum.

Todo pedido será criado no estado Placed.

Os valores numéricos explícitos serão preservados:
Placed = 1, Confirmed = 2 e Canceled = 3.

**Justificativa:** Evitar estados sem comportamento
definido nos requisitos, reduzindo a complexidade
das transições e dos testes.

**Consequências:**
- Draft deixa de existir no enum.
- A entidade Order controlará as transições permitidas.
- A ausência de Draft não elimina a necessidade
  de validar valores inválidos do enum.
- Canceled será um estado terminal.


## ADR-010 — Estoque associado diretamente ao produto

**Status:** Aceita

**Contexto:** O serviço precisa controlar a quantidade
disponível dos produtos durante a confirmação e o
cancelamento de pedidos.

**Decisão:** Manter StockQuantity como propriedade
da entidade Product, sem criar uma entidade Stock separada.

**Justificativa:** Os requisitos não incluem múltiplos
depósitos, lotes ou reservas de estoque.

**Consequências:**
- O modelo de domínio permanece simples.
- A confirmação desconta as quantidades disponíveis do estoque.
- O cancelamento de pedidos confirmados repõe as quantidades anteriormente descontadas.
- A infraestrutura garante atomicidade e controle de concorrência por meio dos mecanismos descritos na ADR-005.
- A separação do estoque poderá ser reconsiderada
  se surgirem novos requisitos.


## ADR-011 — Validação de estoque na criação e na confirmação

**Status:** Aceita

**Contexto:** O serviço permite criar pedidos sem reservar ou
descontar estoque. A disponibilidade pode mudar entre a criação
e a confirmação do pedido.

**Decisão:**
- Na criação, verificar preliminarmente a disponibilidade.
- Registrar o pedido no estado Placed, sem movimentar estoque.
- Na confirmação, verificar e descontar o estoque por meio de
  atualizações condicionais no PostgreSQL.
- Executar a movimentação de estoque e a persistência da
  confirmação na mesma transação.
- Em caso de falha, reverter integralmente a transação.
- Controlar também a concorrência sobre o estado do pedido,
  impedindo descontos duplicados.

**Consequências:**
- Um pedido criado pode falhar na confirmação por falta de estoque.
- A validação preliminar não representa reserva de unidades.
- A confirmação deve ser atômica e idempotente.
- O cancelamento de um pedido confirmado deve repor o estoque
  uma única vez, na mesma transação que persiste o cancelamento.

### ADR-012 — Chave primária composta e integridade referencial de OrderItem

**Status:** Aceita

**Contexto**

Cada pedido possui uma coleção de itens, e cada item representa um produto específico com sua quantidade e o preço unitário capturado no momento da criação.

O caso de uso de criação consolida solicitações repetidas do mesmo produto em um único item. Portanto, um produto deve aparecer no máximo uma vez dentro de cada pedido.

**Decisão**

A tabela `order_items` utilizará a chave primária composta `(order_id, product_id)`, sendo ambos os campos do tipo UUID.

Os dois campos também serão chaves estrangeiras:

- `order_id` referencia `orders(id)`.
- `product_id` referencia `products(id)`.

O relacionamento entre `Order` e `OrderItem` será configurado no Entity Framework Core utilizando `OwnsMany`, mantendo os itens subordinados ao agregado `Order`.

A exclusão física de um produto referenciado por pedidos será restringida, preservando a integridade referencial e o histórico.

Não será criado um identificador artificial adicional para `OrderItem`.

**Consequências**

- O banco impede que um mesmo produto apareça mais de uma vez no mesmo pedido.
- O banco impede referências a pedidos ou produtos inexistentes.
- A identidade de um item depende da combinação do pedido com o produto.
- `OrderItem` não necessita de repositório independente.
- O preço unitário permanece armazenado no item, independentemente de alterações futuras no preço do produto.
- Caso seja necessário permitir múltiplas linhas do mesmo produto no mesmo pedido, a estratégia de identificação deverá ser revista.


## ADR-013 — Organização dos casos de uso da Application

**Status:** Aceita

### Contexto

A camada Application precisa apresentar uma organização consistente,
facilitar a localização dos casos de uso e permitir sua evolução sem
concentrar responsabilidades em classes extensas.

### Decisão

Organizar os casos de uso por funcionalidade, utilizando diretórios
específicos para:

- Commands: solicitações de operações que modificam o estado.
- Queries: solicitações de consulta.
- DTOs: estruturas auxiliares de transferência de dados.
- Responses: resultados retornados pelos casos de uso.
- Handlers: coordenação da execução dos casos de uso.

Manter os diretórios explícitos mesmo quando contiverem apenas um arquivo.

Utilizar handlers simples, sem obrigatoriedade de MediatR ou de um
mecanismo de despacho de comandos.

### Consequências

- Organização previsível e consistente.
- Separação clara entre entrada, execução e saída.
- Facilidade para localizar e testar casos de uso.
- Maior quantidade de diretórios, aceita em benefício da organização.

## ADR-014 — Contratos HTTP independentes da Application

**Status:** Aceita

### Contexto

A API representa uma fronteira de comunicação externa. Expor diretamente
os contratos internos da Application pode acoplar a interface HTTP à
organização dos casos de uso.

### Decisão

Definir DTOs próprios de entrada e saída na camada API.

Converter explicitamente os DTOs de entrada da API em Commands ou Queries
da Application e converter as Responses da Application em contratos HTTP.

Inicialmente, realizar esses mapeamentos com código C#, sem introduzir
bibliotecas adicionais.

A API continuará dependendo da Application para executar os casos de uso,
respeitando a direção das dependências da arquitetura.

### Consequências

- Os contratos HTTP podem evoluir independentemente dos contratos internos.
- Mudanças internas não exigem necessariamente alterações na interface HTTP.
- A fronteira entre API e Application fica explícita.
- Existe algum código adicional de mapeamento, considerado aceitável.

## ADR-015 — Inicialização automática do banco de dados e isolamento dos testes

**Status:** Aceita e implementada

### Contexto

O OrderService utiliza PostgreSQL e Entity Framework Core 10 para persistência.

Os testes de integração precisam validar o comportamento real da infraestrutura, incluindo consultas, transações, atualizações condicionais de estoque e aplicação de migrations.

Também é necessário garantir que os testes sejam reproduzíveis e não dependam de um banco PostgreSQL previamente configurado na máquina do desenvolvedor.

### Decisão

1. Manter as migrations do Entity Framework Core exclusivamente no projeto `OrderService.Infrastructure`, como fonte de versionamento do esquema do banco de dados.

2. Aplicar as migrations pendentes durante a inicialização da API utilizando `Database.MigrateAsync()`.

3. Utilizar **Testcontainers for .NET** para provisionar um contêiner PostgreSQL destinado aos testes de integração.

4. Centralizar o ciclo de vida do contêiner na classe `IntegrationDatabaseFixture`, que implementa `IAsyncLifetime` do xUnit v3.

5. Registrar a fixture no escopo do assembly por meio de `AssemblyFixture`, permitindo compartilhar a infraestrutura de PostgreSQL entre os testes de integração.

6. Carregar as configurações do contêiner a partir de `appsettings.Testing.json`, permitindo sobrescrita por variáveis de ambiente. Essas configurações incluem imagem Docker, nome do banco, usuário e senha.

7. Iniciar o contêiner antes da execução dos testes e obter sua connection string dinamicamente por meio de `GetConnectionString()`.

8. Centralizar a configuração da conexão e a criação do `OrderServiceDbContext` na classe `IntegrationTestDatabase`.

9. Executar `Database.MigrateAsync()` durante a inicialização da fixture, garantindo que o esquema do banco esteja preparado antes dos testes.

10. Encerrar e descartar o contêiner PostgreSQL durante a finalização da fixture.

### Alternativas consideradas

**PostgreSQL previamente instalado ou disponibilizado por Docker Compose:** não adotado para os testes de integração porque exige preparação externa e gerenciamento manual do ambiente.

**Banco de testes separado por sufixo no nome:** estratégia inicialmente considerada, mas substituída pelo provisionamento de PostgreSQL com Testcontainers.

**Banco em memória:** não adotado porque não reproduz integralmente os comportamentos específicos do PostgreSQL, especialmente os mecanismos transacionais e de concorrência utilizados pelo serviço.

### Consequências

**Positivas**

- Os testes utilizam PostgreSQL real, com comportamento compatível com os recursos empregados pela aplicação.
- O banco de integração permanece isolado do banco utilizado pela API.
- O provisionamento do PostgreSQL é automatizado.
- A aplicação das migrations ocorre antes da execução dos testes.
- Não é necessário manter uma instância PostgreSQL de testes previamente configurada.
- O compartilhamento da fixture evita a criação de um contêiner independente para cada classe de teste.
- A infraestrutura pode ser reproduzida em ambientes locais e pipelines que disponibilizem Docker.

**Limitações e cuidados**

- A execução dos testes de integração exige Docker disponível e operacional.
- O primeiro provisionamento pode exigir o download da imagem PostgreSQL.
- O tempo de execução inclui a inicialização do contêiner e a aplicação das migrations.
- O compartilhamento do banco entre testes exige cuidados com o isolamento dos dados e a independência dos cenários.
- A aplicação automática de migrations na inicialização da API não substitui uma estratégia controlada de implantação em ambientes de produção.

### Validação

A implementação foi validada pela execução da suíte automatizada do OrderService utilizando PostgreSQL provisionado por Testcontainers.

Na verificação final da solução em configuração Release:

- 184 testes aprovados.
- Nenhum teste com falha.
- Nenhum teste ignorado.
- Testes de integração executados com PostgreSQL 17.
- Migrations aplicadas automaticamente durante a preparação do banco de integração.

### Referências

- https://dotnet.testcontainers.org/
- https://dotnet.testcontainers.org/modules/postgres/
- https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- https://xunit.net/

## ADR-016 — Organização da camada API por funcionalidades

**Status:** Aceito

### Contexto

A camada API precisa acomodar diferentes funcionalidades, como pedidos, produtos e, futuramente, configurações de negócio.

Uma organização exclusivamente por tipo técnico, com todos os controllers e DTOs em diretórios globais, pode dificultar a localização de componentes relacionados conforme o sistema cresce.

Por outro lado, colocar diretórios de negócio diretamente na raiz da API pode gerar ambiguidades com diretórios de infraestrutura e configuração, como `Configurations`, `DI` e `Extensions`.

Também é necessário considerar a evolução dos controllers, evitando arquivos excessivamente grandes e permitindo sua divisão por responsabilidade quando necessário.

### Decisão

A camada API será organizada utilizando um diretório principal `Features`, responsável por agrupar os componentes específicos das funcionalidades de negócio.

Cada funcionalidade terá seu próprio diretório, como `Features/Orders` ou `Features/Products`.

Os controllers serão mantidos em um subdiretório `Controllers` dentro da respectiva funcionalidade, evitando a concentração de todos os controllers em um diretório global.

Os contratos HTTP serão organizados por caso de uso, utilizando um diretório `DTOs`.

Os DTOs de entrada utilizarão o sufixo `Request`, enquanto os DTOs de saída utilizarão nomes que os diferenciem dos contratos da Application, como `CreateOrderHttpResponse`.

Os diretórios técnicos da API, como `Configurations`, `DI` e `Extensions`, permanecerão separados de `Features`.

Inicialmente, os endpoints relacionados a pedidos serão agrupados em `OrdersController`. Caso sua responsabilidade aumente significativamente, ele poderá ser dividido em controllers menores, sem necessidade de reorganizar os demais componentes.

As rotas HTTP serão declaradas explicitamente, evitando dependência dos nomes dos controllers e possíveis ambiguidades decorrentes de controllers com nomes semelhantes.

### Estrutura de referência

```text
Api/
├── Configurations/
├── DI/
├── Extensions/
├── Features/
│   ├── Auth/
│   │   ├── Controllers/
│   │   ├── GenerateToken/
│   │   │   └── DTOs/
│   │   └── Services/
│   └── Orders/
│       ├── Controllers/
│       ├── CreateOrder/
│       │   ├── DTOs/
│       │   └── Mappings/
│       └── GetOrderById/
│           ├── DTOs/
│           └── Mappings/
└── Program.cs
```

A estrutura acima representa a organização implementada na camada API. As funcionalidades estão agrupadas em Features, enquanto os componentes técnicos permanecem organizados separadamente. Novos diretórios serão introduzidos conforme a necessidade de implementação, evitando estruturas antecipadas.

### Consequências

**Positivas**

- Separa claramente componentes técnicos e funcionalidades de negócio.
- Facilita a localização de controllers, DTOs e mapeamentos relacionados.
- Evita o crescimento de um diretório global contendo todos os controllers.
- Permite dividir controllers por responsabilidade quando necessário.
- Preserva a separação entre contratos HTTP e contratos da Application.
- Favorece o crescimento incremental da API sem introduzir novas camadas arquiteturais.

**Limitações e cuidados**

- A organização física dos arquivos não impede, por si só, conflitos de rotas HTTP.
- A divisão de controllers deverá ocorrer por responsabilidade, e não apenas por quantidade de linhas.
- Não serão criados diretórios ou abstrações para funcionalidades ainda não implementadas.
- A convenção deverá ser mantida de forma consistente nas novas funcionalidades.

### Justificativa

A decisão combina a organização por funcionalidades com o padrão de controllers do ASP.NET Core MVC.

Não se pretende implementar uma arquitetura Vertical Slice completa na camada API, mas utilizar uma organização de código que facilite sua manutenção e evolução, preservando a simplicidade exigida pelo escopo do projeto.

## ADR-017 — Inicialização de dados de desenvolvimento

**Status:** Aceita  
**Data:** 2026-10-19

### Contexto

O OrderService necessita de produtos previamente cadastrados para permitir a execução e validação do endpoint `POST /orders`.

Como o desafio não prevê inicialmente um endpoint de cadastro de produtos, é necessário disponibilizar dados de exemplo de maneira automatizada e reproduzível.

### Decisão

Utilizar a classe `DevelopmentDataSeeder`, localizada no projeto `OrderService.Infrastructure`, para cadastrar três produtos de exemplo com identificadores GUID fixos.

O seed será executado durante a inicialização da API, após a aplicação das migrations, exclusivamente quando o ambiente ASP.NET Core for `Development`.

A implementação deverá:

- Utilizar `Product.Create()`, respeitando as validações do domínio.
- Definir `Currency` como `BRL` nos três produtos de demonstração.
- Verificar a existência dos produtos por identificador antes de inseri-los.
- Inserir somente os produtos ausentes.
- Preservar os dados dos produtos já cadastrados, incluindo alterações de preço e estoque.
- Utilizar o `OrderServiceDbContext` existente, sem introduzir abstrações adicionais.

### Alternativas consideradas

**`HasData()` do EF Core:** não adotado porque vincula os dados às migrations e ao gerenciamento do modelo, o que não é necessário para produtos de demonstração.

**`UseSeeding()` e `UseAsyncSeeding()` do EF Core:** alternativas válidas, mas não adotadas neste momento para manter explícito o controle do ambiente e reduzir a complexidade da implementação.

**Inserção manual por SQL:** não adotada porque exige preparação adicional do banco e dificulta a reprodução dos testes.

### Consequências

**Positivas:**

- Ambiente local preparado automaticamente.
- Requisições do Bruno podem utilizar identificadores conhecidos.
- Reinicializações normais não duplicam os produtos.
- Dados de demonstração não são inseridos pela inicialização da API em ambientes diferentes de `Development`.
- Migrations permanecem responsáveis pela evolução do esquema.

**Limitações:**

- O seed não foi projetado para execução concorrente entre múltiplas instâncias.
- Os produtos são dados de demonstração e não devem ser considerados dados de produção.
- Os dados existentes não são restaurados aos valores originais em reinicializações posteriores.


## ADR-018 — Autenticação JWT sem persistência de usuários

**Status:** Aceita  
**Data:** 2026-10-20

### Contexto

O OrderService disponibiliza autenticação para proteger os endpoints, mas o desafio técnico não exige um módulo completo de identidade, cadastro ou administração de usuários.

### Decisão

Utilizar autenticação baseada em JWT sem persistir usuários ou credenciais em tabelas do PostgreSQL. A implementação de autenticação permanece delimitada ao escopo do desafio.

A origem, a configuração e a validação das credenciais devem ser descritas conforme o código efetivamente implementado; esta decisão não pressupõe um mecanismo específico de armazenamento de segredos.

### Justificativa

Concentrar a solução nas regras de pedidos, estoque e persistência, sem introduzir um subsistema de identidade que não é necessário para o escopo proposto.

### Alternativas consideradas

- Persistência local de usuários e credenciais.
- Integração com um provedor externo de identidade.

Ambas podem ser apropriadas em outros contextos, mas adicionariam responsabilidades não contempladas nesta implementação.

### Consequências e limitações

- Não há cadastro, manutenção ou recuperação de contas de usuários por meio de persistência local.
- Não há revogação individual de credenciais baseada em registros de usuários no banco.
- A emissão e a validação de tokens precisam respeitar a configuração e as políticas de segurança efetivamente implementadas.
- A ausência de persistência de usuários **não é uma recomendação geral para produção**.
- Em uma evolução para produção, devem ser avaliados requisitos de identidade, gestão de segredos, rotação, revogação, autorização e auditoria.

## ADR-019 — Moeda única por pedido e consistência monetária

**Status:** Aceita  
**Data:** 2026-10-20

### Contexto

Os produtos possuem preços monetários e o pedido registra o preço unitário dos itens. Sem uma moeda explícita, valores de moedas distintas poderiam ser somados indevidamente.

### Decisão

- `Product` possui `Price` e `Currency`.
- `Order` possui uma única propriedade `Currency`, definida na criação.
- `OrderItem` preserva `UnitPrice` como valor histórico, sem uma propriedade de moeda própria; os itens utilizam a moeda do pedido.
- Os códigos de moeda aceitos possuem três letras ASCII, com entrada sem distinção entre maiúsculas e minúsculas e armazenamento normalizado em maiúsculas.
- Na criação do pedido, a moeda de cada produto deve corresponder à moeda do pedido. A incompatibilidade produz o erro de negócio `order.currency_mismatch`.
- Não há conversão cambial automática. Produtos com moedas incompatíveis não são convertidos nem somados.
- O preço unitário é obtido do produto durante a criação do pedido; o cliente não o informa no payload.

### Justificativa

Impedir totais monetários inconsistentes e preservar a interpretação histórica dos valores dos itens, mantendo o modelo proporcional ao escopo do desafio.

### Alternativas consideradas

- Permitir moedas diferentes em um mesmo pedido e converter os valores.
- Armazenar a moeda separadamente em cada item.
- Assumir implicitamente uma moeda fixa para todo o sistema.

### Consequências

- O pedido representa valores em uma única moeda, sem ambiguidade na composição do total.
- Alterações futuras no preço do produto não modificam `OrderItem.UnitPrice` de pedidos existentes.
- A validação de compatibilidade deve ser coberta por testes de aplicação e domínio, conforme a responsabilidade de cada camada.
- A inclusão das colunas de moeda é versionada pela migration `20261020131825_AddCurrencyToOrdersAndProducts`, sem apagar o histórico de migrations.
- A recriação do banco realizada durante o desenvolvimento foi uma operação pontual sobre dados descartáveis, não uma política de migração de ambientes com dados persistentes.

---

## Histórico

| Data | Alteração |
|---|---|
| 2026-10-15 | Registro inicial das decisões ADR-001 a ADR-005. |
| 2026-10-15 | Adição da decisão ADR-006. |
| 2026-10-15 | Adição da decisão ADR-007. |
| 2026-10-15 | Adição da decisão ADR-008. |
| 2026-10-15 | Adição da decisão ADR-009. |
| 2026-10-15 | Adição da decisão ADR-010. |
| 2026-10-15 | Adição da decisão ADR-011. |
| 2026-10-15 | Adição da decisão ADR-012. |
| 2026-10-16 | Adição da decisão ADR-013. |
| 2026-10-16 | Adição da decisão ADR-014. |
| 2026-10-19 | Adição da decisão ADR-015. |
| 2026-10-19 | Adição da decisão ADR-016. |
| 2026-10-19 | Adição da decisão ADR-017. |
| 2026-10-20 | Atualização das ADR-004 e ADR-017; inclusão das ADR-018 e ADR-019. |
