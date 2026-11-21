using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace JellyFish.Api.Domain;

public abstract class BaseEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = ObjectId.GenerateNewId().ToString();

    public DateTime CreateDate { get; private set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; private set; } = DateTime.UtcNow;

    public bool? SyncRequired { get; private set; }

    public void Touch() => LastModifiedDate = DateTime.UtcNow;

    public void MarkSynced()
    {
        SyncRequired = false;
        Touch();
    }

    public void MarkChanged()
    {
        SyncRequired = true;
        Touch();
    }
}

