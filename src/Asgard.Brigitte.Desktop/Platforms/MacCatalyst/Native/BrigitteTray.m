#import <AppKit/AppKit.h>

// AppKit 扩展由 macOS SDK 单独编译；不使用 Catalyst 私有 API。
typedef void (*BrigitteCallback)(int,const char *,double,double);
static BrigitteCallback callback;
static NSStatusItem *statusItem;
static NSMutableArray *windowDelegates;

@interface BrigitteWindowDelegate : NSObject <NSWindowDelegate>
@property(nonatomic,weak) id original;
@property(nonatomic,weak) NSWindow *window;
@property(nonatomic) BOOL main;
@end
@implementation BrigitteWindowDelegate
- (BOOL)respondsToSelector:(SEL)selector { return [super respondsToSelector:selector] || [self.original respondsToSelector:selector]; }
- (id)forwardingTargetForSelector:(SEL)selector { return self.original; }
- (BOOL)windowShouldClose:(NSWindow *)window {
    if(self.main && callback) { callback(5,NULL,0,0); return NO; }
    return [self.original respondsToSelector:_cmd] ? [self.original windowShouldClose:window] : YES;
}
@end

@interface BrigitteTrayTarget : NSObject
@end
@implementation BrigitteTrayTarget
- (void)invoke:(NSMenuItem *)sender {
    NSString *project = sender.representedObject;
    if(callback) callback((int)sender.tag,project ? project.UTF8String : NULL,0,0);
}
@end
static BrigitteTrayTarget *target;

static NSImage *BrigitteImage(void) {
    // 与 Windows 共用圣火女神轮廓，模板图像由系统适配菜单栏的明暗背景。
    NSString *path=[NSBundle.mainBundle pathForResource:@"brigid-tray" ofType:@"png"];
    NSImage *image=[[NSImage alloc] initWithContentsOfFile:path];
    image.size=NSMakeSize(22,22);image.template=YES;return image;
}
static NSMenuItem *BrigitteItem(NSString *title,int action,NSString *project) {
    NSMenuItem *item=[[NSMenuItem alloc]initWithTitle:title action:@selector(invoke:) keyEquivalent:@""];
    item.target=target;item.tag=action;item.representedObject=project;return item;
}

__attribute__((visibility("default"))) int BrigitteTrayStart(BrigitteCallback handler) {
    callback=handler;target=[BrigitteTrayTarget new];windowDelegates=[NSMutableArray new];
    statusItem=[NSStatusBar.systemStatusBar statusItemWithLength:NSSquareStatusItemLength];
    statusItem.button.image=BrigitteImage();statusItem.button.toolTip=@"Brigitte · BUG 管理器";
    return statusItem ? 1 : 0;
}
__attribute__((visibility("default"))) void BrigitteTraySetProjects(const char *json) {
    NSData *data=[[NSString stringWithUTF8String:json]dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *value=[NSJSONSerialization JSONObjectWithData:data options:0 error:nil];NSMenu *menu=[NSMenu new];
    if([value[@"authenticated"]boolValue]) {
        for(NSDictionary *project in value[@"projects"]) {
            NSString *name=project[@"name"];NSString *title=[NSString stringWithFormat:@"%@  ·  %@ 未解决",name,project[@"count"]];
            NSMenuItem *item=BrigitteItem(title,4,project[@"id"]);item.toolTip=title;[menu addItem:item];
        }
    } else { [menu addItem:BrigitteItem(@"登录 Brigitte",1,nil)]; }
    [menu addItem:NSMenuItem.separatorItem];[menu addItem:BrigitteItem(@"打开主窗口",1,nil)];
    NSMenuItem *refresh=BrigitteItem(@"刷新",2,nil);refresh.enabled=[value[@"authenticated"]boolValue];[menu addItem:refresh];
    [menu addItem:BrigitteItem(@"退出",3,nil)];statusItem.menu=menu;
}
__attribute__((visibility("default"))) void BrigitteTrayAttachWindow(const char *title,int main) {
    NSString *name=[NSString stringWithUTF8String:title];
    for(NSWindow *window in NSApp.windows) if([window.title isEqualToString:name]) {
        for(BrigitteWindowDelegate *existing in windowDelegates) if(existing.window==window)return;
        BrigitteWindowDelegate *proxy=[BrigitteWindowDelegate new];proxy.original=window.delegate;proxy.window=window;proxy.main=main!=0;
        [windowDelegates addObject:proxy];window.delegate=proxy;return;
    }
}
__attribute__((visibility("default"))) void BrigitteTrayShowWindow(const char *title,int visible) {
    NSString *name=[NSString stringWithUTF8String:title];
    for(NSWindow *window in NSApp.windows) if([window.title isEqualToString:name]) {
        if(visible){[window deminiaturize:nil];[window makeKeyAndOrderFront:nil];[NSApp activateIgnoringOtherApps:YES];}else[window orderOut:nil];return;
    }
}
__attribute__((visibility("default"))) int BrigitteTraySaveFile(const char *name,const unsigned char *bytes,size_t count) {
    NSSavePanel *panel=NSSavePanel.savePanel;panel.nameFieldStringValue=[NSString stringWithUTF8String:name];
    if([panel runModal]!=NSModalResponseOK)return 0;
    NSData *data=[NSData dataWithBytes:bytes length:count];return [data writeToURL:panel.URL options:NSDataWritingAtomic error:nil] ? 1 : 0;
}
// 与 Windows 共用关闭策略，原生对话框仅返回选择和“不再提示”标志。
__attribute__((visibility("default"))) int BrigitteTrayChooseClose(int canUseTray) {
    NSAlert *alert=[NSAlert new];alert.messageText=@"关闭 Brigitte";
    alert.informativeText=canUseTray ? @"退出程序会停止后台同步和通知。最小化到托盘会继续运行。可在“设置 → 普通设置”中更改关闭行为。" : @"系统托盘暂不可用。你可以退出程序，或取消并保留窗口。";
    NSButton *tray=[alert addButtonWithTitle:@"最小化到托盘"];tray.enabled=canUseTray!=0;tray.keyEquivalent=@"";
    [alert addButtonWithTitle:@"退出程序"];
    NSButton *cancel=[alert addButtonWithTitle:@"取消"];cancel.keyEquivalent=@"\r";
    alert.showsSuppressionButton=YES;alert.suppressionButton.title=@"以后不再提示，记住这次选择";
    NSModalResponse result=[alert runModal];
    int action=result==NSAlertFirstButtonReturn ? 1 : result==NSAlertSecondButtonReturn ? 2 : 0;
    return action==0 ? 0 : action | (alert.suppressionButton.state==NSControlStateValueOn ? 4 : 0);
}
__attribute__((visibility("default"))) void BrigitteTrayStop(void) {
    for(BrigitteWindowDelegate *proxy in windowDelegates)if(proxy.window.delegate==proxy)proxy.window.delegate=proxy.original;
    [windowDelegates removeAllObjects];if(statusItem)[NSStatusBar.systemStatusBar removeStatusItem:statusItem];statusItem=nil;callback=NULL;
}
