using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Example
{

    internal class User
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }
    }

    internal class UserImportProvider : ProducerConsumerImportProvider<User>
    {
        private readonly HttpClient client;

        public UserImportProvider(HttpClient client, ILogger<UserImportProvider> logger) : base(logger)
        {
            this.client = client;
        }

        protected override Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        public override Task<bool> CanImportAsync(SchemaType type)
        {
            return Task.FromResult(type.Name == "user");
        }

        protected override Task<AttributeChange> CreateAttributeChangeAsync(SchemaAttribute type, ObjectModificationType modificationType, User item)
        {
            switch (type.Name)
            {
                case "name":
                    return Task.FromResult(AttributeChange.CreateAttributeAdd(type.Name, item.Name));

                case "email":
                    return Task.FromResult(AttributeChange.CreateAttributeAdd(type.Name, item.Email));

                case "phone":
                    return Task.FromResult(AttributeChange.CreateAttributeAdd(type.Name, item.Phone));
            }

            return Task.FromResult<AttributeChange>(null);
        }

        protected override Task<List<AnchorAttribute>> GetAnchorAttributesAsync(User item)
        {
            List<AnchorAttribute> anchors = new List<AnchorAttribute>();
            anchors.Add(AnchorAttribute.Create("id", item.Id));
            return Task.FromResult(anchors);
        }

        protected override Task<string> GetDNAsync(User item)
        {
            return Task.FromResult(item.Id);
        }

        protected override Task<ObjectModificationType> GetObjectModificationTypeAsync(User item)
        {
            return Task.FromResult(ObjectModificationType.Add);
        }

        protected override async IAsyncEnumerable<User> GetObjects()
        {
            var result = await this.client.GetAsync(this.client.BaseAddress + "/users");

            result.EnsureSuccessStatusCode();

            var usersData = await result.Content.ReadAsStringAsync();

            var users = JsonSerializer.Deserialize<List<User>>(usersData);

            foreach (User user in users)
            {
                yield return user;
            }
        }
    }
}
