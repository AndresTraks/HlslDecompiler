xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.5, 0, 0, 0
dcl_texcoord0 r0
/*    0.0 */       cjmp !b128, L4
/*    0.1 */       loop i16, L4
                label L2
/*    1.0 */       exec    // PredicateClean=false
/*    3   */          sgt r1.x___, r0.xxxx, c[0+aL].xxxx
/*    4   */          setp_ne r0._, r1.x
/*    5   */     (p0) add r0, r0, -c[0+aL]
/*    6   */    (!p0) mad r0, c[0+aL], c255.xxxx, r0
/*    1.1 */       endloop i16, L2
                label L4
/*    2.0 */       alloc colors
/*    2.1 */       exece
/*    7   */          max oC0, r0, r0
