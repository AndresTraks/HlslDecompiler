xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=0
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.5, 0.25, 0, 0
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    2   */          tfetch2D r0.zwxy, r0.xyx, tf0
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    3   */          mul oC0.__zw, r0.xxxy, c0.zzzw
/*    4   */          mul r0.xy__, r0.yyyy, c255.xyyy
/*    5   */          mad oC0.xy__, r0.zwww, c0.xyyy, r0.xyyy
/*    1.1 */       cnop
