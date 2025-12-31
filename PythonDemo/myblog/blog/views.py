from django.shortcuts import render, get_object_or_404, redirect
from django.core.paginator import Paginator
from django.contrib import messages
from django.http import JsonResponse
from django.template.loader import render_to_string
from .models import Post, Category, Comment


def post_list(request):
    """文章列表视图"""
    posts = Post.objects.filter(published=True).select_related('author', 'category')
    categories = Category.objects.all()
    
    # 分页
    paginator = Paginator(posts, 6)
    page = request.GET.get('page')
    posts = paginator.get_page(page)
    
    # 检查是否为 AJAX 请求
    if request.headers.get('X-Requested-With') == 'XMLHttpRequest':
        html = render_to_string('blog/partials/post_list_content.html', {
            'posts': posts,
            'categories': categories,
        }, request=request)
        return JsonResponse({'html': html, 'title': '首页 - 我的博客'})
    
    return render(request, 'blog/post_list.html', {
        'posts': posts,
        'categories': categories,
    })


def post_detail(request, slug):
    """文章详情视图"""
    post = get_object_or_404(Post, slug=slug, published=True)
    
    # 增加阅读量
    post.views += 1
    post.save(update_fields=['views'])
    
    # 获取评论
    comments = post.comments.filter(approved=True)
    
    # 获取相关文章
    related_posts = Post.objects.filter(
        category=post.category, 
        published=True
    ).exclude(id=post.id)[:3]
    
    context = {
        'post': post,
        'comments': comments,
        'related_posts': related_posts,
    }
    
    # 检查是否为 AJAX 请求
    if request.headers.get('X-Requested-With') == 'XMLHttpRequest':
        html = render_to_string('blog/partials/post_detail_content.html', context, request=request)
        return JsonResponse({'html': html, 'title': f'{post.title} - 我的博客'})
    
    return render(request, 'blog/post_detail.html', context)


def category_posts(request, slug):
    """分类文章列表视图"""
    category = get_object_or_404(Category, slug=slug)
    posts = Post.objects.filter(category=category, published=True)
    categories = Category.objects.all()
    
    # 分页
    paginator = Paginator(posts, 6)
    page = request.GET.get('page')
    posts = paginator.get_page(page)
    
    context = {
        'category': category,
        'posts': posts,
        'categories': categories,
    }
    
    # 检查是否为 AJAX 请求
    if request.headers.get('X-Requested-With') == 'XMLHttpRequest':
        html = render_to_string('blog/partials/category_posts_content.html', context, request=request)
        return JsonResponse({'html': html, 'title': f'{category.name} - 我的博客'})
    
    return render(request, 'blog/category_posts.html', context)


def add_comment(request, slug):
    """添加评论视图"""
    post = get_object_or_404(Post, slug=slug, published=True)
    
    if request.method == 'POST':
        author_name = request.POST.get('author_name', '').strip()
        author_email = request.POST.get('author_email', '').strip()
        content = request.POST.get('content', '').strip()
        
        if author_name and content:
            Comment.objects.create(
                post=post,
                author_name=author_name,
                author_email=author_email,
                content=content
            )
            
            # AJAX 请求返回 JSON
            if request.headers.get('X-Requested-With') == 'XMLHttpRequest':
                comments = post.comments.filter(approved=True)
                html = render_to_string('blog/partials/comments_list.html', {
                    'comments': comments,
                }, request=request)
                return JsonResponse({'success': True, 'html': html, 'message': '评论发表成功！'})
            
            messages.success(request, '评论发表成功！')
        else:
            if request.headers.get('X-Requested-With') == 'XMLHttpRequest':
                return JsonResponse({'success': False, 'message': '请填写昵称和评论内容'})
            messages.error(request, '请填写昵称和评论内容')
    
    return redirect('blog:post_detail', slug=slug)
