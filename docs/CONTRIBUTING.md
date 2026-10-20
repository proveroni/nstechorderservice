# Padrões de contribuição

Este documento estabelece as convenções de documentação adotadas no projeto OrderService.

## Padrões de documentação

### Observações e avisos

Utilizar **GitHub Alerts** como padrão para destacar observações, recomendações e avisos nos arquivos Markdown (`.md`) do projeto.

| Tipo | Finalidade |
|---|---|
| `[!NOTE]` | Observações e informações complementares. |
| `[!TIP]` | Recomendações e boas práticas. |
| `[!IMPORTANT]` | Informações essenciais para a execução ou compreensão do projeto. |
| `[!WARNING]` | Alertas sobre riscos e cuidados necessários. |
| `[!CAUTION]` | Riscos de consequências negativas. |

### Exemplo de utilização

```markdown
> [!NOTE]
> Senhas com caracteres especiais podem exigir tratamento específico
> na connection string do Npgsql.
>
> Recomenda-se utilizar uma senha aleatória, longa e alfanumérica.
```

### Diretrizes

- Utilizar `[!NOTE]` como padrão para observações.
- Escolher os demais tipos conforme a finalidade da mensagem.
- Evitar blocos de alerta para informações comuns que possam ser apresentadas em parágrafos.
- Manter os textos objetivos e claros.
- Priorizar a compatibilidade com a renderização Markdown do GitHub.
