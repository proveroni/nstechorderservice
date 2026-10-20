using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Features.Orders.CreateOrder.DTOs;
using OrderService.Api.Features.Orders.CreateOrder.Mappings;
using OrderService.Api.Features.Orders.GetOrderById.DTOs;
using OrderService.Api.Features.Orders.GetOrderById.Mappings;
using OrderService.Application.Orders.Operations.CancelOrder.Commands;
using OrderService.Application.Orders.Operations.CancelOrder.Handlers;
using OrderService.Application.Orders.Operations.ConfirmOrder.Commands;
using OrderService.Application.Orders.Operations.ConfirmOrder.Handlers;
using OrderService.Application.Orders.Operations.CreateOrder.Handlers;
using OrderService.Application.Orders.Queries.GetOrderById.Handlers;
using OrderService.Application.Orders.Queries.GetOrderById.Queries;
using OrderService.Application.Orders.Queries.GetOrders.Handlers;
using OrderService.Application.Orders.Queries.GetOrders.Queries;

namespace OrderService.Api.Features.Orders.Controllers
{
    /// <summary>
    /// Disponibiliza os endpoints HTTP relacionados aos pedidos.
    /// </summary>
    [ApiController]
    [Route("orders")]
    [Authorize]
    public sealed class OrdersController : ControllerBase
    {
        private readonly CreateOrderHandler _createOrderHandler;
        private readonly GetOrderByIdHandler _getOrderByIdHandler;
        private readonly ConfirmOrderHandler _confirmOrderHandler;
        private readonly CancelOrderHandler _cancelOrderHandler;
        private readonly GetOrdersHandler _getOrdersHandler;


        /// <summary>
        /// Inicializa o controller com as dependências necessárias.
        /// </summary>
        /// <param name="createOrderHandler">Handler responsável pela criação de pedidos.</param>
        /// <param name="getOrderByIdHandler">Handler responsável pela consulta por identificador.</param>
        /// <param name="confirmOrderHandler">Handler responsável pela confirmação de pedidos.</param>
        /// <param name="cancelOrderHandler">Handler responsável pelo cancelamento de pedidos.</param>
        /// <param name="getOrdersHandler">Handler responsável pela listagem paginada de pedidos.</param>
        public OrdersController(
            CreateOrderHandler createOrderHandler,
            GetOrderByIdHandler getOrderByIdHandler,
            ConfirmOrderHandler confirmOrderHandler,
            CancelOrderHandler cancelOrderHandler,
            GetOrdersHandler getOrdersHandler)
        {
            ArgumentNullException.ThrowIfNull(createOrderHandler);
            ArgumentNullException.ThrowIfNull(getOrderByIdHandler);
            ArgumentNullException.ThrowIfNull(confirmOrderHandler);
            ArgumentNullException.ThrowIfNull(cancelOrderHandler);
            ArgumentNullException.ThrowIfNull(getOrdersHandler);

            _createOrderHandler = createOrderHandler;
            _getOrderByIdHandler = getOrderByIdHandler;
            _confirmOrderHandler = confirmOrderHandler;
            _cancelOrderHandler = cancelOrderHandler;
            _getOrdersHandler = getOrdersHandler;
        }

        /// <summary>
        /// Cria um novo pedido.
        /// </summary>
        /// <param name="request">Dados do pedido solicitado.</param>
        /// <param name="cancellationToken">
        /// Token de cancelamento da requisição.
        /// </param>
        /// <returns>Resultado da tentativa de criação do pedido.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(CreateOrderHttpResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAsync(
            [FromBody] CreateOrderRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest(new
                {
                    Code = "order.invalid_request",
                    Message = "Os dados do pedido são obrigatórios."
                });
            }

            var command = CreateOrderRequestMapper.ToCommand(request);

            var result = await _createOrderHandler.HandleAsync(
                command,
                cancellationToken);

            if (result.IsFailure)
            {
                var errors = result.Errors;

                var statusCode = errors.Any(error =>
                    error.Code == "order.insufficient_stock")
                    ? StatusCodes.Status409Conflict
                    : errors.Any(error =>
                        error.Code == "order.product_not_found")
                        ? StatusCodes.Status404NotFound
                        : StatusCodes.Status400BadRequest;

                return StatusCode(statusCode, new
                {
                    Errors = errors
                });
            }

            var response = CreateOrderResponseMapper.ToHttpResponse(
                result.Value);

            return StatusCode(StatusCodes.Status201Created, response);
        }

        /// <summary>
        /// Consulta pedidos utilizando filtros opcionais e paginação.
        /// </summary>
        /// <param name="customerId">Identificador opcional do cliente.</param>
        /// <param name="status">Situação opcional do pedido.</param>
        /// <param name="from">Data inicial de criação, inclusive.</param>
        /// <param name="to">Data final de criação, inclusive.</param>
        /// <param name="page">Número da página, iniciado em 1.</param>
        /// <param name="pageSize">Quantidade de pedidos por página.</param>
        /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
        /// <returns>Pedidos encontrados e informações da paginação.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetOrdersAsync(
            [FromQuery] Guid? customerId,
            [FromQuery] OrderService.Domain.Enums.OrderStatusEnum? status,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var query = new GetOrdersQuery
            {
                CustomerId = customerId,
                Status = status,
                From = from,
                To = to,
                Page = page,
                PageSize = pageSize
            };

            var result = await _getOrdersHandler.HandleAsync(
                query,
                cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(new
                {
                    Errors = result.Errors
                });
            }

            return Ok(result.Value);
        }

        /// <summary>
        /// Consulta um pedido pelo seu identificador.
        /// </summary>
        /// <param name="id">Identificador do pedido.</param>
        /// <param name="cancellationToken">
        /// Token de cancelamento da requisição.
        /// </param>
        /// <returns>
        /// Dados do pedido encontrado ou os erros da consulta.
        /// </returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(
            typeof(GetOrderByIdHttpResponse),
            StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByIdAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var query = new GetOrderByIdQuery(id);

            var result = await _getOrderByIdHandler.HandleAsync(
                query,
                cancellationToken);

            if (result.IsFailure)
            {
                var statusCode = result.Errors.Any(error =>
                    error.Code == "order.not_found")
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;

                return StatusCode(statusCode, new
                {
                    Errors = result.Errors
                });
            }

            var response = GetOrderByIdResponseMapper.ToHttpResponse(
                result.Value);

            return Ok(response);
        }

        /// <summary>
        /// Confirma um pedido existente.
        /// </summary>
        /// <param name="id">Identificador do pedido.</param>
        /// <param name="cancellationToken">
        /// Token de cancelamento da requisição.
        /// </param>
        /// <returns>
        /// Resultado da tentativa de confirmação do pedido.
        /// </returns>
        [HttpPost("{id:guid}/confirm")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ConfirmAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ConfirmOrderCommand(id);

            var result = await _confirmOrderHandler.HandleAsync(
                command,
                cancellationToken);

            if (result.IsFailure)
            {
                var errors = result.Errors;

                var statusCode = errors.Any(error =>
                    error.Code is "order.already_canceled"
                        or "order.insufficient_stock")
                    ? StatusCodes.Status409Conflict
                    : errors.Any(error =>
                        error.Code == "order.not_found")
                        ? StatusCodes.Status404NotFound
                        : StatusCodes.Status400BadRequest;

                return StatusCode(statusCode, new
                {
                    Errors = errors
                });
            }

            return NoContent();
        }

        /// <summary>
        /// Cancela um pedido existente.
        /// </summary>
        /// <param name="id">Identificador do pedido.</param>
        /// <param name="cancellationToken">
        /// Token de cancelamento da requisição.
        /// </param>
        /// <returns>
        /// Resultado da tentativa de cancelamento do pedido.
        /// </returns>
        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelAsync(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var command = new CancelOrderCommand(id);

            var result = await _cancelOrderHandler.HandleAsync(
                command,
                cancellationToken);

            if (result.IsFailure)
            {
                var errors = result.Errors;

                var statusCode = errors.Any(error =>
                    error.Code == "product.stock_restoration_failed")
                    ? StatusCodes.Status409Conflict
                    : errors.Any(error =>
                        error.Code == "order.not_found")
                        ? StatusCodes.Status404NotFound
                        : StatusCodes.Status400BadRequest;

                return StatusCode(statusCode, new
                {
                    Errors = errors
                });
            }

            return NoContent();
        }
    }
}
