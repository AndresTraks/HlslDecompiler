xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.1, 0, 1.5, 0
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    3   */          sgt r1.x___, c0.xxxx, r0.xxxx
/*    4   */          setp_ne r0._, r1.x
/*    0.1 */       loop i31, L4
                label L2
/*    1.0 */  (p0) exec    // PredicateClean=false
/*    5   */     (p0) mad r0, r0, c255.zzzz, c255.xxxx
/*    6   */     (p0) sgt r1.x___, c0.xxxx, r0.xxxx
/*    7   */     (p0) setp_ne r0._, r1.x
/*    1.1 */ (!p0) endloop i31, L2
                label L4
/*    2.0 */       alloc colors
/*    2.1 */       exece
/*    8   */          max oC0, r0, r0
