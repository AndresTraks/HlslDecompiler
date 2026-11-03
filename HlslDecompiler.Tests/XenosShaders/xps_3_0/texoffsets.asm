xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=5
dcl_texcoord0 r0.xy
dcl_texcoord1 r1.x
/*    0.0 */       exec
/*    2   */          tfetch2D r2, r0.xyx, tf1, LODBias=0.5
/*    3   */          tfetch2D r5, r0.xyx, tf0, OffsetX=0.5, OffsetY=-0.5
/*    4   */          tfetch2D r3, r0.yx, tf0, OffsetX=-0.5, OffsetY=0.5
/*    5   */          tfetch1D r1, r1.x, tf2, OffsetX=0.5
/*    6   */          tfetch2D r0, r0.yx, tf1, LODBias=-0.5, OffsetX=1.5, OffsetY=1.5
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    7   */          add r4.x___, r1.xxxx, r0.xxxx
/*    8   */          add r3, r5, r3
/*    9   */          mad oC0.x___, r3.xxxx, r2.xxxx, r4.xxxx
/*   10   */          mad r0.xyz_, r3.yzww, r2.yzww, r0.yzww
/*   11   */          add oC0._yzw, r0.xxyz, r1.yyzw
/*    1.1 */       cnop
