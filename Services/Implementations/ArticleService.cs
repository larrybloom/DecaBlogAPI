﻿using DevsTutorialCenterAPI.Data;
using DevsTutorialCenterAPI.Data.Entities;
using DevsTutorialCenterAPI.Data.Repositories;
using DevsTutorialCenterAPI.Models.DTOs;
using DevsTutorialCenterAPI.Models.Enums;
using DevsTutorialCenterAPI.Services.Abstractions;
using DevsTutorialCenterAPI.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevsTutorialCenterAPI.Services.Implementations;

public class ArticleService : IArticleService
{
    private readonly IRepository _repository;
    private readonly IArticleApprovalService _articleApprovalService;
    private readonly ITagService _tagService;
    private readonly DevsTutorialCenterAPIContext _db;
    private readonly SignInManager<AppUser> _signInManager;

    public ArticleService(IRepository repository, IArticleApprovalService articleApprovalService,
        ITagService tagService, DevsTutorialCenterAPIContext db, SignInManager<AppUser> signInManager)
    {
        _signInManager = signInManager;
        _repository = repository;
        _articleApprovalService = articleApprovalService;
        _tagService = tagService;
        _db = db;
    }


    public async Task<Article> GetArticleById(string articleId)
    {
        return await _repository.GetByIdAsync<Article>(articleId);
    }

    //DONE
    public async Task<UpdateArticleDto> UpdateArticleAsync(string articleId, UpdateArticleDto updatedArticle)
    {
        var existingArticle = await _repository.GetByIdAsync<Article>(articleId);

        if (existingArticle == null)
        {
            throw new Exception($"Article with ID {articleId} not found.");
        }

        existingArticle.Title = updatedArticle.Title ?? existingArticle.Title;
        existingArticle.TagId = updatedArticle.TagId ?? existingArticle.TagId;
        existingArticle.Text = updatedArticle.Text ?? existingArticle.Text;
        existingArticle.ImageUrl = updatedArticle.ImageUrl ?? existingArticle.ImageUrl;


        await _repository.UpdateAsync<Article>(existingArticle);

        var updatedArticleDto = new UpdateArticleDto
        {
            Title = existingArticle.Title,
            TagId = existingArticle.TagId,
            Text = existingArticle.Text,
            ImageUrl = existingArticle.ImageUrl
        };

        return updatedArticleDto;
    }

    //DONE (GOES WITH GETSINGLE ARTICLE)
    public async Task LogArticleReadAsync(string articleId, string userId)
    {
        var getArticleByArticleId = await _repository.GetByIdAsync<Article>(articleId);

        if (getArticleByArticleId == null)
        {
            throw new Exception("Article not found");
        }

        var articleRead = new ArticleRead
        {
            UserId = userId,
            ArticleId = articleId
        };

        await _repository.AddAsync<ArticleRead>(articleRead);

        getArticleByArticleId.ReadCount++;

        await _repository.UpdateAsync<Article>(getArticleByArticleId);
    }

    public async Task<GetSingleArticleDto?> GetSingleArticle(string articleId, string? userId = null)
    {
        var article = await _repository.GetByIdAsync<Article>(articleId);
        var likes = (await _repository.GetAllAsync<ArticleLike>())
            .Where(a => a.ArticleId == articleId);
        var likeCount = await likes.CountAsync();
        var comments = await (await _repository.GetAllAsync<Comment>())
            .Include(comment => comment.User)
            .Where(comment => comment.ArticleId == articleId)
            .Select(comment => new CommentDto
            {
                Id = comment.Id,
                Text = comment.Text,
                Name = $"{comment.User.FirstName} {comment.User.LastName}",
                Image = comment.User.ImageUrl,
                CreatedOn = comment.CreatedOn,
            })
            .ToListAsync();

        if (article == null)
            return null;

        var tag = await _repository.GetByIdAsync<ArticleTag>(article.TagId);

        var articleDto = new GetSingleArticleDto
        {
            Id = article.Id,
            PublicId = article.PublicId,
            AuthorId = article.AuthorId,
            Title = article.Title,
            TagId = article.TagId,
            TagName = tag?.Name,
            Text = article.Text,
            ImageUrl = article.ImageUrl,
            ReadCount = article.ReadCount,
            ReadTime = article.ReadTime,
            CreatedOn = article.CreatedOn,
            Comments = comments,
            LikeCount = likeCount,
        };
        
        if (userId is not null)
        {
            articleDto.Liked = await likes.Where(l => l.UserId == userId).AnyAsync();
            articleDto.Bookmarked = await (await _repository.GetAllAsync<ArticleBookMark>())
                .Where(x => x.UserId == userId && x.ArticleId == articleId)
                .AnyAsync();
            
            var user = await _repository.GetByIdAsync<AppUser>(userId);
            if (user == null) throw new Exception($"User with ID {userId} not found.");
            await LogArticleReadAsync(article.Id, user.Id);
        }

        return articleDto;
    }


    //DONE
    public async Task<CreateArticleDto> CreateArticleAsync(CreateArticleDto model)
    {
        //var readtimeresult = Helper.CalculateReadingTime(model.Text);


        var tag = await _tagService.GetByIdAsync<ArticleTag>(model.TagId);

        if (tag == null)
        {
            throw new Exception("Tag  not found.");
        }

        var newArticle = new Article
        {
            Title = model.Title,
            TagId = tag.Id,
            Text = model.Text,
            ImageUrl = model.ImageUrl,
        };


        await _repository.AddAsync<Article>(newArticle);
        var articleApproval = new ArticleApproval
        {
            ArticleId = newArticle.Id,
            Status = ApprovalStatusConstant.Pending
        };

        await _articleApprovalService.ApproveAsync(articleApproval);

        var newArticleData = new CreateArticleDto
        {
            Title = newArticle.Title,
            TagId = newArticle.TagId,
            Text = newArticle.Text,
            ImageUrl = newArticle.ImageUrl
        };

        return newArticleData;
    }

    public async Task<CreateArticleDtoReturn> CreateArticleAsync2(CreateArticleDto2 model)
    {
        var readtimeresult = Helper.CalculateReadingTime(model.Text);

        var tag = await _tagService.GetByIdAsync<ArticleTag>(model.TagId);

        if (tag == null)
        {
            throw new Exception("Tag not found.");
        }

        var userId = _signInManager.UserManager.GetUserId(_signInManager.Context.User);

        var newArticle = new Article
        {
            Title = model.Title,
            TagId = tag.Id,
            Text = model.Text,
            ImageUrl = model.ImageUrl,
            AuthorId = userId,
            ReadCount = 0,
            PublicId = model.PublicId,
            ReadTime = $"{readtimeresult} mins",
        };


        await _repository.AddAsync<Article>(newArticle);
        newArticle.CreatedOn = DateTime.UtcNow;
        newArticle.UpdatedOn = DateTime.UtcNow;
        var articleApproval = new ArticleApproval
        {
            ArticleId = newArticle.Id,
            Status = ApprovalStatusConstant.Pending
        };

        await _articleApprovalService.ApproveAsync(articleApproval);

        var newArticleData = new CreateArticleDtoReturn
        {
            Id = newArticle.Id,
            Title = newArticle.Title,
            TagId = newArticle.TagId,
            Text = newArticle.Text,
            ImageUrl = newArticle.ImageUrl,
            AuthorId = newArticle.AuthorId,
            PublicId = newArticle.PublicId,
            ReadCount = newArticle.ReadCount,
            ReadTime = newArticle.ReadTime
        };

        return newArticleData;
    }

    public async Task<PaginatorResponseDto<IEnumerable<GetAllArticlesDto>>> GetAllArticles(FilterArticleDto filters)
    {
        // var articles = await _repository.GetAllAsync<Article>();
        var articles = (await _repository.GetAllAsync<ArticleApproval>())
            .Where(a => a.Status == ApprovalStatusConstant.IsPublished)
            .Include(a => a.Article)
            .ThenInclude(a => a.Tag)
            .Select(a => a.Article)
            .Where(a => !a.IsDeleted);
        
        if (!string.IsNullOrEmpty(filters.AuthorId))
        {
            articles = articles.Where(a => a.AuthorId == filters.AuthorId);
        }

        if (!string.IsNullOrEmpty(filters.TagId))
        {
            articles = articles.Where(a => a.TagId == filters.TagId);
        }
        
        if (filters.IsRecentlyAdded.GetValueOrDefault())
        {
            articles = articles.OrderByDescending(a => a.CreatedOn);
        }
        else if (filters.IsTopRead.GetValueOrDefault())
        {
            articles = articles.OrderByDescending(a => a.ReadCount);
        }

        var articlesDto = articles
            .Select(a => new GetAllArticlesDto
            {
                Id = a.Id,
                Title = a.Title,
                Text = a.Text,
                AuthorId = a.AuthorId,
                AuthorImage = a.Author.ImageUrl,
                AuthorName = a.Author.FirstName + " " + a.Author.LastName,
                TagId = a.TagId,
                TagName = a.Tag.Name,
                ReadCount = a.ReadCount,
                ImageUrl = a.ImageUrl,
                PublicId = a.PublicId,
                ReadTime = a.ReadTime,
                IsDeleted = a.IsDeleted,
                CreatedOn = a.CreatedOn,
                IsRecentlyAdded = filters.IsRecentlyAdded.GetValueOrDefault(),
                IsTopRead = filters.IsTopRead.GetValueOrDefault(),
            });

        var pageNum = filters.Page ?? 1;
        var pageSize = filters.Size ?? 10;

        var paginatorResponse = Helper.Paginate(articlesDto, pageNum, pageSize);

        return paginatorResponse;
    }

    public async Task<IEnumerable<GetAllArticlesDto>> GetBookmarkedArticles(string userId)
    {
        var bookmarkedArticles = await (await _repository.GetAllAsync<ArticleBookMark>())
            .Where(b => b.UserId == userId)
            .Include(b => b.Article)
            .ThenInclude(a => a.Tag)
            .Select(a => new GetAllArticlesDto
            {
                Id = a.Article.Id,
                Title = a.Article.Title,
                Text = a.Article.Text,
                AuthorId = a.Article.AuthorId,
                AuthorName = a.Article.Author.FirstName + " " + a.Article.Author.LastName,
                AuthorImage = a.Article.Author.ImageUrl,
                TagId = a.Article.TagId,
                TagName = a.Article.Tag.Name,
                ReadCount = a.Article.ReadCount,
                ImageUrl = a.Article.ImageUrl,
                PublicId = a.Article.PublicId,
                ReadTime = a.Article.ReadTime,
                IsDeleted = a.Article.IsDeleted,
                CreatedOn = a.Article.CreatedOn,
            })
            .ToListAsync();

        return bookmarkedArticles;
    }


    public async Task<bool> SetArticleReportStatus(string articleId, string status)
    {
        if (status != ArticleStatusReportEnum.Approved.ToString().ToLower() &&
            status != ArticleStatusReportEnum.Declined.ToString().ToLower())
        {
            throw new Exception("Invalid status provided");
        }

        var article = await _repository.GetByIdAsync<Article>(articleId);

        if (article == null)
        {
            throw new Exception("Article not found");
        }

        //if(article.IsReported && status == ArticleStatusReportEnum.Approved.ToString().ToLower())
        //{
        //    throw new Exception("Article already approved");
        //}

        //if (!article.IsReported && status == ArticleStatusReportEnum.Declined.ToString().ToLower())
        //{
        //    throw new Exception("Article already declined");
        //}


        //if (status == ArticleStatusReportEnum.Approved.ToString().ToLower())
        //{
        //    article.IsReported = true;
        //}


        //else if (status == ArticleStatusReportEnum.Declined.ToString().ToLower())
        //{
        //    article.IsReported= false;
        //}

        await _repository.UpdateAsync<Article>(article);

        return true;
    }


    public async Task<bool> DeleteArticleAsync(string articleId)
    {
        var article = await _repository.GetByIdAsync<Article>(articleId);

        await _repository.DeleteAsync(article);
        return true;
    }

    public async Task<List<LikesByArticleDto>> GetLikesByArticleAsync(string articleId)
    {
        // Retrieve likes by article ID using the repository
        var likes = await _repository.GetAllAsync<ArticleLike>();

        // Filter the likes by the specified articleId
        var likesWithArticleId = likes
            .Where(like => like.ArticleId == articleId)
            .Select(like => new LikesByArticleDto
            {
                UserId = like.UserId,
                ArticleId = like.ArticleId
            })
            .ToList();

        if (!likesWithArticleId.Any()) return new List<LikesByArticleDto>();

        return likesWithArticleId;
    }

    public async Task<AuthorsStatsDto> GetAuthorStatsAsync(FetchAuthorsStatsDto fetchAuthorsStatsDto)
    {
        var authorStats = new List<AuthorStatsDto>();

        foreach (var authorId in fetchAuthorsStatsDto.AuthorIdList)
        {
            var query = await _repository.GetAllAsync<Article>();
            query = query.Where(a => a.AuthorId == authorId);

            var authorStat = new AuthorStatsDto
            {
                AuthorId = authorId,
                TotalNumOfArticles = query.Count(),
                // TotalReportedArticles = query.Where(a => a.IsReported).Count()
            };

            authorStats.Add(authorStat);
        }

        return new AuthorsStatsDto
        {
            AuthorStatsDtos = authorStats
        };
    }

    public async Task<IEnumerable<Article>> GetAllArticle()
    {
        var articles = (await _repository.GetAllAsync<Article>())
            .Include(a => a.Tag)
            .Include(a => a.Author);

        return articles;
    }

    public async Task<bool> IsArticleBookmarkedByUser(string articleId, string userId)
    {
        var bookmarkedArticle = await _repository.GetAllAsync<ArticleBookMark>();

        bool isBookmarked = bookmarkedArticle.Any(b => b.ArticleId == articleId && b.UserId == userId);

        return isBookmarked;
    }

    public async Task<List<GetPendingArticlesDto>> GetPendingArticles()
    {
        //var filter = (await _repository.GetAllAsync<ArticleApproval>()).Where(x => x.Status == SD.pending);
        var articleApprovals = await _repository.GetAllAsync<ArticleApproval>();

        var filter = articleApprovals.Where(x => x.Status == 1);

        if (filter == null)
        {
            throw new ArgumentNullException("No pending articles");
        }

        var getAllPendingArticles = new List<GetPendingArticlesDto>();

        foreach (var article in filter)
        {
            var foundArticle = await GetArticleById(article.ArticleId);
            var getPendingArticle = new GetPendingArticlesDto
            {
                Title = foundArticle is not null ? foundArticle.Title : null,
                Text = foundArticle is not null ? foundArticle.Text : null,
                TagId = foundArticle is not null ? foundArticle.TagId : null,
                ImageUrl = foundArticle is not null ? foundArticle.ImageUrl : null,
            };

            getAllPendingArticles.Add(getPendingArticle);
        }


        return getAllPendingArticles;
    }

    public async Task<object> SoftDeleteArticle(string articleId)
    {
        var article = await _repository.GetByIdAsync<Article>(articleId);

        if (article == null) throw new Exception("Article not Found");

        article.DeletedAt = DateTime.UtcNow;

        await _repository.UpdateAsync<Article>(article);

        return new { deletedAt = article.DeletedAt };
    }

    public async Task<List<AuthorDTO>> GetAuthorStats()
    {
        var articles = await _repository.GetAllAsync2<Article>();

        var authors = new List<AuthorDTO>();

        foreach (var art in articles)
        {
            var user = await _repository.GetByIdAsync<AppUser>(art.AuthorId);

            var articlesByUser = (await _repository.GetAllAsync2<Article>()).Where(a => a.AuthorId == user.Id);

            if (user is not null)
            {
                var author = new AuthorDTO
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Squad = user.Squad,
                    Stack = user.Stack,
                    PhoneNumber = user.PhoneNumber,
                    ImageUrl = user.ImageUrl,
                    NoOfArticles = articlesByUser.Count()
                };

                authors.Add(author);
            }
        }

        return authors;
    }

    public async Task<IEnumerable<GetAllArticlesDto>> GetAuthorArticles(string authorId)
    {
        var articles = (await _repository.GetAllAsync<ArticleApproval>())
            .Where(a => a.Status == ApprovalStatusConstant.IsPublished)
            .Include(a => a.Article)
            .ThenInclude(a => a.Tag)
            // .Select(a => a.Article)
            .Select(a => new GetAllArticlesDto
            {
                Id = a.Article.Id,
                Title = a.Article.Title,
                Text = a.Article.Text,
                AuthorId = a.Article.AuthorId,
                AuthorImage = a.Article.Author.ImageUrl,
                AuthorName = a.Article.Author.FirstName + " " + a.Article.Author.LastName,
                TagId = a.Article.TagId,
                TagName = a.Article.Tag.Name,
                ReadCount = a.Article.ReadCount,
                ImageUrl = a.Article.ImageUrl,
                PublicId = a.Article.PublicId,
                ReadTime = a.Article.ReadTime,
                IsDeleted = a.Article.IsDeleted,
                CreatedOn = a.Article.CreatedOn,
                ApprovalStatus = a.Status
            })
            .OrderByDescending(a => a.CreatedOn);

        return articles;
    }
   
    public async Task<IEnumerable<GetAllArticlesDto>> SearchArticles(string searchTerm)
    {
        var articles = (await _repository.GetAllAsync<Article>())
            .Include(a => a.Tag)
            .Include(a => a.Author)
            .Where(a =>
               (a.Author.FirstName + " " + a.Author.LastName).Contains(searchTerm) ||
                a.Author.FirstName.Contains(searchTerm) ||
                a.Author.LastName.Contains(searchTerm) ||
                a.Title.Contains(searchTerm) ||
                a.Tag.Name.Contains(searchTerm)
            )
            .Select(a => new GetAllArticlesDto
            {
                Id = a.Id,
                Title = a.Title,
                Text = a.Text,
                AuthorId = a.AuthorId,
                AuthorImage = a.Author.ImageUrl,
                AuthorName = a.Author.FirstName + " " + a.Author.LastName,
                TagId = a.TagId,
                TagName = a.Tag.Name,
                ReadCount = a.ReadCount,
                ImageUrl = a.ImageUrl,
                PublicId = a.PublicId,
                ReadTime = a.ReadTime,
                IsDeleted = a.IsDeleted,
                CreatedOn = a.CreatedOn,
            });

        return articles;
    }
}