xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0, 0, 0, 0
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    2   */          sgt r1.x___, r0.xxxx, c0.xxxx
/*    3   */          setp_ne_push r1.x___, c255.xxxx, r1.xxxx
/*    4   */     (p0) add r0, r0.wxyz, r0.wxyz
/*    5   */          setp_inv r1.x___, r1.x
/*    6   */     (p0) sgt r1.x___, r0.yyyy, c0.yyyy
/*    7   */     (p0) setp_eq r0._, r1.x
/*    0.1 */  (p0) exec
/*    8   */     (p0) add r0, -r0.wxyz, c0.wxyz
/*    1.0 */       alloc colors
/*    1.1 */       exece
/*    9   */          max oC0, r0.yzwx, r0.yzwx
