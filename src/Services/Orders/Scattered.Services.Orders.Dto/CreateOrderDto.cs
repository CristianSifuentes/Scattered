using Scattered.Services.Products.Dtos;

namespace Scattered.Services.Orders.Dto;


public record OrderDto(Guid orderId, OrderAddress orderAddress, PersonalDetails PersonalDetails, List<FullProductResponse> Products);
public record CreateOrderDto(OrderAddress orderAddress, PersonalDetails PersonalDetails, List<FullProductResponse> Products);
