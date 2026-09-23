#import <UIKit/UIKit.h>

extern "C" void KVA_PlayImpact(int style, float intensity)
{
    // Unity invokes this on its main thread. Retain one generator per style for bursts.
    if (@available(iOS 10.0, *))
    {
        if ([UIApplication sharedApplication].applicationState != UIApplicationStateActive) return;
        static UIImpactFeedbackGenerator *generators[3];
        const int index = MAX(0, MIN(style, 2));
        if (generators[index] == nil)
        {
            const UIImpactFeedbackStyle styles[] = {
                UIImpactFeedbackStyleLight, UIImpactFeedbackStyleMedium, UIImpactFeedbackStyleHeavy
            };
            generators[index] = [[UIImpactFeedbackGenerator alloc] initWithStyle:styles[index]];
            [generators[index] prepare];
        }
        if (@available(iOS 13.0, *))
            [generators[index] impactOccurredWithIntensity:MAX(0.0f, MIN(intensity, 1.0f))];
        else
            [generators[index] impactOccurred];
        [generators[index] prepare];
    }
}
