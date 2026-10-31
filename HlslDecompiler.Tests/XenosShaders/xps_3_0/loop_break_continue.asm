xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=3
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0, 10, 0, 0
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    4   */          sgt r3, -r_abs[0].xxxx, c255.xxxx
/*    5   */          sgt r1, -r_abs[0].xxxx, c255.xxxx
              +       setp_rstr r2.x___, c255.x
/*    0.1 */       loop i16, L5
                label L2
/*    1.0 */       exec    // PredicateClean=false
/*    6   */          setp_ge_push r2.x___, r2.xxxx, c[0+aL].wwww
/*    7   */     (p0) mad r1, r0, c[0+aL], r1
/*    8   */     (p0) sgt r2._y__, r1.xxxx, c255.yyyy
/*    9   */          setp_ne_push r2.x___, r2.xxxx, r2.yyyy
/*   10   */     (p0) setp_clr r2.x___
/*   11   */          setp_pop r2.x___, r2.x
/*    1.1 */       exec    // PredicateClean=false
/*   12   */     (p0) max r3, r1.wzyx, r1.wzyx
/*   13   */          setp_pop r2.x___, r2.x
/*   14   */     (p0) max r3, r1.wzyx, r1.wzyx
/*   15   */    (!p0) setp_clr r2.x___
/*    2.0 */ (!p0) endloop i16, L2
                label L5
/*    2.1 */       exec    // PredicateClean=false
/*   16   */          max r0, r3.wzyx, r3.wzyx
              +       setp_rstr r0._, c255.x
/*    3.0 */       alloc colors
/*    3.1 */       exece
/*   17   */          max oC0, r0, r0
