using System.Buffers.Binary;
using System.Security.Cryptography;
using AnySidebar.Core;
namespace AnySidebar.Infrastructure;
public sealed class AttachmentStore:IAttachments {
 readonly string directory;
 public AttachmentStore(string root){directory=Path.Combine(root,"images");Directory.CreateDirectory(directory);}
 public ImageAsset Persist(byte[] png,SourceInfo source,byte[]? thumbnail=null){var(w,h)=ValidatePng(png);if(png.Length>10485760)throw new AppFault("IMAGE_LIMIT","单张截图最多 10 MiB，请框选更小区域。");var id=J.Id();var file=Path.Combine(directory,id+".png");var temporary=file+".tmp";try{File.WriteAllBytes(temporary,png);File.Move(temporary,file);if(thumbnail is not null){ValidatePng(thumbnail);File.WriteAllBytes(Path.Combine(directory,id+".thumb.png"),thumbnail);}return new(){Id=id,Source=source,Width=w,Height=h,ByteLength=png.Length,Sha256=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant(),StorageKey=id+".png",ThumbnailKey=thumbnail is null?null:id+".thumb.png"};}catch{if(File.Exists(temporary))File.Delete(temporary);if(File.Exists(file))File.Delete(file);throw;}}
 public byte[] Read(ImageAsset asset,bool thumbnail=false){
  if(!J.IsId(asset.Id)||asset.StorageKey!=asset.Id+".png"||(asset.ThumbnailKey is not null&&asset.ThumbnailKey!=asset.Id+".thumb.png"))throw new AppFault("IMAGE_MISSING","无效截图标识。");
  var path=Path.Combine(directory,thumbnail&&asset.ThumbnailKey is not null?asset.ThumbnailKey:asset.StorageKey);
  try{var bytes=File.ReadAllBytes(path);ValidatePng(bytes);if(!thumbnail&&(bytes.Length!=asset.ByteLength||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=asset.Sha256))throw new InvalidDataException();return bytes;}catch{throw new AppFault("IMAGE_MISSING","截图文件缺失或校验失败，请重新截取。");}
 }
 public void Delete(ImageAsset asset){if(!J.IsId(asset.Id))return;foreach(var suffix in new[]{".png",".thumb.png",".png.tmp"}){var file=Path.Combine(directory,asset.Id+suffix);try{File.Delete(file);}catch{/* Garbage collection retries later. */}}}
 public void Collect(IReadOnlySet<string> liveIds){foreach(var file in Directory.EnumerateFiles(directory)){var name=Path.GetFileName(file);var id=name.Split('.')[0];if(J.IsId(id)&&!liveIds.Contains(id))try{File.Delete(file);}catch{/* Keep files that cannot be deleted; never follow unknown paths. */}}}
 public static (int Width,int Height) ValidatePng(byte[] data){
  ReadOnlySpan<byte> signature=[137,80,78,71,13,10,26,10];if(data.Length<45||data.Length>10485760||!data.AsSpan(0,8).SequenceEqual(signature))throw new AppFault("INVALID_IMAGE","截图不是有效的 PNG。");
  int offset=8,width=0,height=0;bool first=true,end=false,hasData=false;
  while(offset<=data.Length-12){var size=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset,4));if(size>data.Length-offset-12)throw new AppFault("INVALID_IMAGE","PNG 数据不完整。");var length=(int)size;var kind=System.Text.Encoding.ASCII.GetString(data,offset+4,4);var expected=BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset+8+length,4));if(Crc(data.AsSpan(offset+4,length+4))!=expected)throw new AppFault("INVALID_IMAGE","PNG 校验失败。");
   if(first){if(kind!="IHDR"||length!=13)throw new AppFault("INVALID_IMAGE","PNG 缺少图像尺寸。");width=BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset+8,4));height=BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset+12,4));if(width is <1 or >8192||height is <1 or >8192)throw new AppFault("IMAGE_LIMIT","截图尺寸须在 1–8192px 内。");first=false;}
   else if(kind=="IHDR")throw new AppFault("INVALID_IMAGE","PNG 含重复头部。");
   if(kind=="IDAT")hasData=true;if(kind=="IEND"){if(length!=0||offset+12!=data.Length)throw new AppFault("INVALID_IMAGE","PNG 结束标记异常。");end=true;break;}offset+=length+12;
  }
  if(!end||!hasData)throw new AppFault("INVALID_IMAGE","PNG 数据不完整。");return(width,height);
 }
 static uint Crc(ReadOnlySpan<byte> bytes){uint crc=uint.MaxValue;foreach(var b in bytes){crc^=b;for(var bit=0;bit<8;bit++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}return ~crc;}
}
