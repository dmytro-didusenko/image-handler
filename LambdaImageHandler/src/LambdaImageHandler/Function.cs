using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaImageHandler
{
    public class Function
    {
        const string BUCKET_NAME = "five-technical-friday-220923";
        const string INPUT_PREFIX = "input/";
        const string OUTPUT_PREFIX = "output/";
        const int IMAGE_SIDE = 600;

        IAmazonS3 S3Client { get; set; }

        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        public Function(IAmazonS3 s3Client)
        {
            S3Client = s3Client;
        }

        public async Task FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var eventRecords = evnt.Records ?? new List<S3Event.S3EventNotificationRecord>();
            foreach (var record in eventRecords)
            {
                var s3Event = record.S3;
                if (s3Event == null) continue;

                try
                {
                    var fileName = s3Event.Object.Key;

                    context.Logger.LogInformation($"[{DateTime.Now}] File \"{fileName}\" processing - START");

                    var file = await S3Client.GetObjectAsync(BUCKET_NAME, fileName);

                    var image = await Image.LoadAsync(file.ResponseStream);
                    var format = image.Metadata.DecodedImageFormat
                        ?? throw new ArgumentNullException($"Error getting format from file: {fileName}");

                    var cropWidth = image.Width > IMAGE_SIDE ? IMAGE_SIDE : image.Width;
                    var cropHeight = image.Height > IMAGE_SIDE ? IMAGE_SIDE : image.Height;
                    var x = (image.Width - IMAGE_SIDE) / 2;
                    var y = (image.Height - IMAGE_SIDE) / 2;

                    var croppedImage = image.Clone(i => i.Crop(new Rectangle(x, y, cropWidth, cropHeight)));

                    var outStream = new MemoryStream();
                    croppedImage.Save(outStream, format);

                    await S3Client.PutObjectAsync(
                        new PutObjectRequest
                        {
                            BucketName = BUCKET_NAME,
                            Key = $"{OUTPUT_PREFIX}{fileName?.Replace(INPUT_PREFIX, string.Empty)}",
                            InputStream = outStream
                        }
                    );

                    context.Logger.LogInformation($"[{DateTime.Now}] File \"{fileName}\" processing - SUCCESS");
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}.");
                    context.Logger.LogError(ex.Message);
                    context.Logger.LogError(ex.StackTrace);
                    throw;
                }
            }
        }
    }
}