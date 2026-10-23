/* Test-only X11 clipboard owner, run on an isolated Xvfb server. */
#include <X11/Xlib.h>
#include <X11/Xatom.h>
#include <stdio.h>
#include <string.h>
int main(int argc, char** argv)
{
    if (argc != 2) return 1;
    Display* display = XOpenDisplay(NULL); if (!display) return 2;
    Window owner = XCreateSimpleWindow(display, DefaultRootWindow(display),0,0,1,1,0,0,0);
    Atom clipboard = XInternAtom(display,"CLIPBOARD",False), utf8 = XInternAtom(display,"UTF8_STRING",False), incr = XInternAtom(display,"INCR",False);
    XSetSelectionOwner(display,clipboard,owner,CurrentTime); XSync(display,False);
    puts("READY"); fflush(stdout);
    const unsigned char unicode[] = "\xc5\xbd\xc4\x85 \xe6\x97\xa5\xe6\x9c\xac \xf0\x9f\x98\x80";
    const unsigned char latin[] = { 'c','a','f',0xe9,' ',0xa3,0 };
    Window requestor = 0; Atom property = 0; int offset = 0;
    for (;;)
    {
        XEvent ev; XNextEvent(display,&ev);
        if (ev.type == SelectionRequest)
        {
            XSelectionRequestEvent* request = &ev.xselectionrequest;
            XEvent reply = {0}; reply.xselection.type = SelectionNotify; reply.xselection.display=display; reply.xselection.requestor=request->requestor;
            reply.xselection.selection=request->selection; reply.xselection.target=request->target; reply.xselection.time=request->time; reply.xselection.property=None;
            if (!strcmp(argv[1],"latin1") && request->target == XA_STRING)
            { XChangeProperty(display,request->requestor,request->property,XA_STRING,8,PropModeReplace,latin,sizeof(latin)-1); reply.xselection.property=request->property; }
            if (strcmp(argv[1],"latin1") && request->target == utf8)
            {
                reply.xselection.property=request->property;
                if (!strcmp(argv[1],"incr"))
                {
                    requestor=request->requestor; property=request->property; offset=0;
                    XSelectInput(display,requestor,PropertyChangeMask);
                    unsigned long length = sizeof(unicode)-1;
                    XChangeProperty(display,requestor,property,incr,32,PropModeReplace,(unsigned char*)&length,1);
                }
                else XChangeProperty(display,request->requestor,request->property,utf8,8,PropModeReplace,unicode,sizeof(unicode)-1);
            }
            XSendEvent(display,request->requestor,False,0,&reply); XFlush(display);
        }
        if (ev.type == PropertyNotify && ev.xproperty.window == requestor && ev.xproperty.atom == property && ev.xproperty.state == PropertyDelete)
        {
            int length = (int)sizeof(unicode)-1-offset;
            if (length > 2) length=2; // intentionally split UTF-8 sequences
            XChangeProperty(display,requestor,property,utf8,8,PropModeReplace,unicode+offset,length);
            offset+=length; XFlush(display);
            if (!length) requestor=0;
        }
    }
}
