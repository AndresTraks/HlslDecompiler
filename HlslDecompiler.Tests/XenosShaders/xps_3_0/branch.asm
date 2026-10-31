xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    3   */          sgt r1.x___, r0.xxxx, c0.xxxx
/*    4   */          setp_ne r0._, r1.x
/*    0.1 */ (!p0) jmp L3
/*    1.0 */  (p0) exec
/*    5   */     (p0) sqrt r1.x___, r_abs[0].x
/*    6   */     (p0) sqrt r1._y__, r_abs[0].y
/*    7   */     (p0) sqrt r1.__z_, r_abs[0].z
/*    8   */     (p0) sqrt r1.___w, r_abs[0].w
/*    9   */     (p0) mul r0, r1.wxyz, c0.wxyz
                label L3
/*    1.1 */       alloc colors
/*    2.0 */       exece
/*   10   */          max oC0, r0.yzwx, r0.yzwx
/*    2.1 */       cnop
