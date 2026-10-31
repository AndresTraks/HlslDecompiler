xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
dcl_index r0.x
dcl_texcoord0 o0.xy
/*    0.0 */       exec
/*    3   */          vfetch r1, r0.x, position0
/*    4   */          vfetch r0.xy__, r0.x, texcoord0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    5   */          max oPos, r1, r1
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    6   */          dp2add o0.x___, r0.yxxx, c0.yxxx, c0.wwww
/*    7   */          dp2add o0._y__, r0.yxxx, c1.yxxx, c1.wwww
/*    2.1 */       cnop
