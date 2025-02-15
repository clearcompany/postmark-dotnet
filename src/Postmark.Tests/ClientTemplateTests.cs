﻿using Xunit;
using PostmarkDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postmark.Tests
{
    public class ClientTemplateTests : ClientBaseFixture, IDisposable
    {
        protected override void Setup()
        {
            _client = new PostmarkClient(WRITE_TEST_SERVER_TOKEN, BASE_URL);
        }

        [Fact]
        public async void ClientCanCreateTemplate()
        {
            var name = Guid.NewGuid().ToString();
            var subject = "A subject: " + Guid.NewGuid();
            var htmlbody = "<b>Hello, {{name}}</b>";
            var textBody = "Hello, {{name}}!";

            var newTemplate = await _client.CreateTemplateAsync(name, subject, htmlbody, textBody);

            Assert.Equal(name, newTemplate.Name);
        }

        [Fact]
        public async void ClientCanEditTemplate()
        {
            var name = Guid.NewGuid().ToString();
            var subject = "A subject: " + Guid.NewGuid();
            var htmlbody = "<b>Hello, {{name}}</b>";
            var textBody = "Hello, {{name}}!";

            var newTemplate = await _client.CreateTemplateAsync(name, subject, htmlbody, textBody);

            var existingTemplate = await _client.GetTemplateAsync(newTemplate.TemplateId);

            await _client.EditTemplateAsync(existingTemplate.TemplateId, name + name, subject + subject, htmlbody + htmlbody, textBody + textBody);

            var updatedTemplate = await _client.GetTemplateAsync(existingTemplate.TemplateId);

            Assert.Equal(existingTemplate.Name + existingTemplate.Name, updatedTemplate.Name);
            Assert.Equal(existingTemplate.HtmlBody + existingTemplate.HtmlBody, updatedTemplate.HtmlBody);
            Assert.Equal(existingTemplate.Subject + existingTemplate.Subject, updatedTemplate.Subject);
            Assert.Equal(existingTemplate.TextBody + existingTemplate.TextBody, updatedTemplate.TextBody);
        }

        [Fact]
        public async void ClientCanDeleteTemplate()
        {
            var name = Guid.NewGuid().ToString();
            var subject = "A subject: " + Guid.NewGuid();
            var htmlbody = "<b>Hello, {{name}}</b>";
            var textBody = "Hello, {{name}}!";

            var newTemplate = await _client.CreateTemplateAsync(name, subject, htmlbody, textBody);
            await _client.DeleteTemplateAsync(newTemplate.TemplateId);
            var deletedTemplate = await _client.GetTemplateAsync(newTemplate.TemplateId);

            Assert.False(deletedTemplate.Active);
        }

        [Fact]
        public async void ClientCanGetTemplate()
        {
            var name = Guid.NewGuid().ToString();
            var subject = "A subject: " + Guid.NewGuid();
            var htmlbody = "<b>Hello, {{name}}</b>";
            var textBody = "Hello, {{name}}!";

            var newTemplate = await _client.CreateTemplateAsync(name, subject, htmlbody, textBody);

            var result = await _client.GetTemplateAsync(newTemplate.TemplateId);

            Assert.Equal(name, result.Name);
            Assert.Equal(htmlbody, result.HtmlBody);
            Assert.Equal(textBody, result.TextBody);
            Assert.Equal(subject, result.Subject);
            Assert.True(result.Active);
            Assert.True(result.AssociatedServerId > 0);
            Assert.Equal(newTemplate.TemplateId, result.TemplateId);
        }

        [Fact]
        public async void ClientCanGetListTemplates()
        {
            for (var i = 0; i < 10; i++)
            {
                await _client.CreateTemplateAsync("test " + i, "test subject" + i, "body");
            }

            var result = await _client.GetTemplatesAsync();
            Assert.Equal(10, result.TotalCount);
            var toDelete = result.Templates.First().TemplateId;
            await _client.DeleteTemplateAsync(toDelete);
            result = await _client.GetTemplatesAsync();
            Assert.Equal(9, result.TotalCount);
            Assert.False(result.Templates.Any(k => k.TemplateId == toDelete));
            var offsetResults = await _client.GetTemplatesAsync(5);
            Assert.True(result.Templates.Skip(5).Select(k => k.TemplateId).SequenceEqual(offsetResults.Templates.Select(k => k.TemplateId)));

        }

        [Fact]
        public async void ClientCanValidateTemplate()
        {
            var result = await _client.ValidateTemplateAsync("{{name}}", "<html><body>{{content}}{{company.address}}{{#each products}}{{/each}}{{^competitors}}There are no substitutes.{{/competitors}}</body></html>", "{{content}}", new { name = "Johnny", content = "hello, world!" });

            Assert.True(result.AllContentIsValid);
            Assert.True(result.HtmlBody.ContentIsValid);
            Assert.True(result.TextBody.ContentIsValid);
            Assert.True(result.Subject.ContentIsValid);
            var inferredAddress = result.SuggestedTemplateModel.company.address;
            var products = result.SuggestedTemplateModel.products;
            Assert.NotNull(inferredAddress);
            Assert.Equal(3, products.Length);
        }

        [Fact]
        public async void ClientCanSendWithTemplate()
        {
            var template = await _client.CreateTemplateAsync("test template name", "test subject", "test html body");
            var sendResult = await _client.SendEmailWithTemplateAsync(template.TemplateId, new { name = "Andrew" }, WRITE_TEST_SENDER_EMAIL_ADDRESS, WRITE_TEST_SENDER_EMAIL_ADDRESS, false);
            Assert.NotEqual(Guid.Empty, sendResult.MessageID);
        }

        [Fact]
        public async void ClientCantSendBatchWithInvalidTemplateAlias()
        {
            var sendResult = await Client.SendMessagesAsync(new[]
            {
                new TemplatedPostmarkMessage
                {
                    TemplateAlias = "invalid-alias",
                    To = WriteTestSenderEmailAddress,
                    From = WriteTestSenderEmailAddress
                }
            });

            Assert.Equal(Guid.Empty, sendResult.Single().MessageID);
            Assert.Equal(1101, sendResult.Single().ErrorCode);
            Assert.Equal("The Template's 'Alias' associated with this request is not valid or was not found.", sendResult.Single().Message);
            Assert.Equal(PostmarkStatus.Unknown, sendResult.Single().Status);
        }

        [Fact]
        public async void ClientCanSendTemplateWithStringModel()
        {
            var template = await _client.CreateTemplateAsync("test template name", "test subject", "test html body");
            var sendResult = await _client.SendEmailWithTemplateAsync(template.TemplateId, "{ \"name\" : \"Andrew\" }", WRITE_TEST_SENDER_EMAIL_ADDRESS, WRITE_TEST_SENDER_EMAIL_ADDRESS, false);
            Assert.NotEqual(Guid.Empty, sendResult.MessageID);
        }

        private Task Cleanup(){
            return Task.Run(async () =>
            {
                try
                {
                    var tasks = new List<Task>();
                    var templates = await _client.GetTemplatesAsync();

                    foreach (var t in templates.Templates)
                    {
                        tasks.Add(_client.DeleteTemplateAsync(t.TemplateId));
                    }
                    await Task.WhenAll(tasks);
                }
                catch { }
            });
        }

        public void Dispose()
        {
            Cleanup().Wait();
        }
    }
}
