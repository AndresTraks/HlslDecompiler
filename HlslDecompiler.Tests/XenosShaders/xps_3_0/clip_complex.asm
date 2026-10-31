xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0, 0, 0, 0
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    2   */          tfetch2D r0, r0.xyx, tf0
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    3   */          subsc r1.x___, -c0.x, -r0.w
/*    4   */          kill_gt r0._, c255.xxxx, r1.xxxx
/*    5   */          max oC0, r0, r0
/*    1.1 */       cnop
