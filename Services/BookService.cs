using Marten;
using Grpc.Core;
using GrpcServiceProject.Models;

namespace GrpcServiceProject.Services
{
    public class BookServiceImpl : GrpcServiceProject.BookService.BookServiceBase
    {
        private readonly ILogger<BookServiceImpl> _logger;
        private readonly IDocumentSession _session;

        public BookServiceImpl(ILogger<BookServiceImpl> logger, IDocumentSession session)
        {
            _logger = logger;
            _session = session;
        }

        public override async Task<BookReply> SaveBook(BookRequest request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    _logger.LogWarning("SaveBook called with empty title");
                    return new BookReply 
                    { 
                        Success = false, 
                        Message = "Title cannot be empty" 
                    };
                }

                if (request.Pages <= 0)
                {
                    _logger.LogWarning("SaveBook called with invalid page count: {Pages}", request.Pages);
                    return new BookReply 
                    { 
                        Success = false, 
                        Message = "Pages must be greater than 0" 
                    };
                }

                var bookDetail = new Book
                {
                    Title = request.Title,
                    Pages = request.Pages
                };

                _session.Store(bookDetail);
                await _session.SaveChangesAsync(context.CancellationToken);

                _logger.LogInformation("Book saved successfully: {Title}, {Pages} pages", 
                    bookDetail.Title, bookDetail.Pages);

                return new BookReply 
                { 
                    Success = true, 
                    Message = $"Book '{bookDetail.Title}' saved successfully with {bookDetail.Pages} pages" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving book: {Title}", request.Title);

                return new BookReply 
                { 
                    Success = false, 
                    Message = $"Failed to save book: {ex.Message}" 
                };
            }
        }

        public override async Task<GetBookReply> GetBookById(GetBookRequest request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.BookId))
                {
                    _logger.LogWarning("GetBookById called with empty book ID");
                    return new GetBookReply
                    {
                        Success = false,
                        Message = "Book ID cannot be empty"
                    };
                }

                if (!Guid.TryParse(request.BookId, out var bookId))
                {
                    _logger.LogWarning("GetBookById called with invalid GUID: {BookId}", request.BookId);
                    return new GetBookReply
                    {
                        Success = false,
                        Message = "Invalid book ID format"
                    };
                }

                var book = await _session.LoadAsync<Book>(bookId, context.CancellationToken);

                if (book == null)
                {
                    _logger.LogInformation("Book not found: {BookId}", bookId);
                    return new GetBookReply
                    {
                        Success = false,
                        Message = $"Book with ID '{bookId}' not found"
                    };
                }

                _logger.LogInformation("Book retrieved: {BookId}, Title: {Title}", book.Id, book.Title);

                return new GetBookReply
                {
                    Success = true,
                    Message = "Book retrieved successfully",
                    Book = new BookData
                    {
                        Id = book.Id.ToString(),
                        Title = book.Title,
                        Pages = book.Pages
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving book: {BookId}", request.BookId);

                return new GetBookReply
                {
                    Success = false,
                    Message = $"Failed to retrieve book: {ex.Message}"
                };
            }
        }
    }
}
