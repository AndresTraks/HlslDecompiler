xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, -0.5, 1, 0, 0
def c255, 0.25, 0.5, 0.75, 0
dcl_texcoord0 r0
/*    0.0 */       exec    // PredicateClean=false
/*    5   */          sge r1.xy__, r0.zyyy, c255.wwww
              +       maxs r0._, r0.xx
/*    6   */          mul r1._y__, r1.xxxx, r1.yyyy
              +       adds_prev r1.x___, c254.y
/*    7   */          cndeq r2._y__, r1.yyyy, r0.xxxx, r1.xxxx
/*    8   */          setp_ne_push r1.x___, c255.wwww, r1.yyyy
              +       sgts r1.__z_, -r_abs[0].x
/*    9   */     (p0) maxs r2.___w, r0.zz
/*   10   */          setp_rstr r2.x___, r1.x
/*    0.1 */       loop i31, L5
                label L2
/*    1.0 */       exec    // PredicateClean=false
/*   11   */          setp_ne_push r2.x___, r2.xxxx, r1.yyyy
/*   12   */     (p0) max r1.___w, r0.wwww, r0.wwww
              +  (p0) maxs r2.__z_, r2.ww
/*   13   */     (p0) add r2._y_w, r2.yyzz, c254.xxyy
              +  (p0) maxs r1.x___, r0.xx
/*   14   */     (p0) maxs r1._y__, r0.yy
/*   15   */     (p0) add r0.xy_w, r1.xyww, c255.xyzz
/*   16   */     (p0) dp3 r1.x___, r1.wxyy, c255.zxyy
              +  (p0) sgts r1._y__, r2.y
/*    1.1 */       exec    // PredicateClean=false
/*   17   */     (p0) mad r1.__z_, r1.xxxx, r2.zzzz, r1.zzzz
/*   18   */          setp_pop r2.x___, r2.x
/*   19   */     (p0) setp_ne r0._, r1.y
/*   20   */    (!p0) setp_clr r2.x___
/*    2.0 */ (!p0) endloop i31, L2
                label L5
/*    2.1 */       exec    // PredicateClean=false
/*   21   */          setp_ge r0._, r0.z
/*    3.0 */ (!p0) jmp L8
/*    3.1 */  (p0) exec
/*   22   */     (p0) maxs r2._y__, c255.yy
                label L8
/*    4.0 */       alloc colors
/*    4.1 */       exece
/*   23   */          add oC0, r2.yyyy, r1.zzzz
