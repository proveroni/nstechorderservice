namespace OrderService.CrossCutting.Results
{
    /// <summary>
    /// Representa o resultado de uma consulta paginada.
    /// </summary>
    /// <typeparam name="T">
    /// Tipo dos elementos retornados pela consulta.
    /// </typeparam>
    public sealed class PagedResult<T>
    {
        /// <summary>
        /// Elementos pertencentes à página solicitada.
        /// </summary>
        public required IReadOnlyList<T> Items { get; init; }

        /// <summary>
        /// Número da página solicitada, iniciado em 1.
        /// </summary>
        public required int Page { get; init; }

        /// <summary>
        /// Quantidade máxima de elementos por página.
        /// </summary>
        public required int PageSize { get; init; }

        /// <summary>
        /// Quantidade total de elementos que atendem aos filtros,
        /// independentemente da paginação.
        /// </summary>
        public required int TotalCount { get; init; }

        /// <summary>
        /// Quantidade total de páginas disponíveis.
        /// </summary>
        public int TotalPages =>
            PageSize > 0
                ? (int)Math.Ceiling((double)TotalCount / PageSize)
                : 0;
    }
}
