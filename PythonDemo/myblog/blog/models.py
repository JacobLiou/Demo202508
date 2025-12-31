from django.db import models
from django.contrib.auth.models import User
from django.urls import reverse
from django.utils.text import slugify


class Category(models.Model):
    """文章分类模型"""
    name = models.CharField('分类名称', max_length=100)
    slug = models.SlugField('URL别名', unique=True, blank=True)
    description = models.TextField('描述', blank=True)
    created_at = models.DateTimeField('创建时间', auto_now_add=True)

    class Meta:
        verbose_name = '分类'
        verbose_name_plural = '分类'
        ordering = ['name']

    def __str__(self):
        return self.name

    def save(self, *args, **kwargs):
        if not self.slug:
            self.slug = slugify(self.name, allow_unicode=True)
        super().save(*args, **kwargs)

    def get_absolute_url(self):
        return reverse('blog:category_posts', kwargs={'slug': self.slug})


class Post(models.Model):
    """博客文章模型"""
    title = models.CharField('标题', max_length=200)
    slug = models.SlugField('URL别名', unique=True, blank=True)
    content = models.TextField('内容')
    excerpt = models.TextField('摘要', max_length=500, blank=True)
    author = models.ForeignKey(
        User, 
        on_delete=models.CASCADE, 
        verbose_name='作者',
        related_name='posts'
    )
    category = models.ForeignKey(
        Category, 
        on_delete=models.SET_NULL, 
        null=True, 
        blank=True,
        verbose_name='分类',
        related_name='posts'
    )
    cover_image = models.URLField('封面图片URL', blank=True)
    created_at = models.DateTimeField('创建时间', auto_now_add=True)
    updated_at = models.DateTimeField('更新时间', auto_now=True)
    published = models.BooleanField('已发布', default=False)
    views = models.PositiveIntegerField('阅读量', default=0)

    class Meta:
        verbose_name = '文章'
        verbose_name_plural = '文章'
        ordering = ['-created_at']

    def __str__(self):
        return self.title

    def save(self, *args, **kwargs):
        if not self.slug:
            self.slug = slugify(self.title, allow_unicode=True)
        if not self.excerpt and self.content:
            self.excerpt = self.content[:200] + '...' if len(self.content) > 200 else self.content
        super().save(*args, **kwargs)

    def get_absolute_url(self):
        return reverse('blog:post_detail', kwargs={'slug': self.slug})


class Comment(models.Model):
    """评论模型"""
    post = models.ForeignKey(
        Post, 
        on_delete=models.CASCADE, 
        verbose_name='文章',
        related_name='comments'
    )
    author_name = models.CharField('昵称', max_length=100)
    author_email = models.EmailField('邮箱', blank=True)
    content = models.TextField('评论内容')
    created_at = models.DateTimeField('创建时间', auto_now_add=True)
    approved = models.BooleanField('已审核', default=True)

    class Meta:
        verbose_name = '评论'
        verbose_name_plural = '评论'
        ordering = ['-created_at']

    def __str__(self):
        return f'{self.author_name} - {self.post.title[:20]}'
