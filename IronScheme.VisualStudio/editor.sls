; License
; Copyright (c) 2016-2021 Llewellyn Pritchard
; All rights reserved.
; This source code is subject to terms and conditions of the BSD License.

(library (editor)
  (export
    view
    set-view
    open-views)
  (import 
    (ironscheme)
    (ironscheme clr internal))

  (define-syntax clr-using
    (lambda (e)
      (syntax-case e ()
        [(_ namespace)
         #'(define using (clr-using-internal 'namespace))])))

  (trace-define-syntax clr-call
    (lambda (e)
      (syntax-case e ()
        [(_ type member instance args ...)
        (writeln e)
         #'(clr-call-internal 'type 'member instance args ...)])))

  (clr-using IronScheme.VisualStudio)
  (clr-using Microsoft.VisualStudio.Text.Operations)

  (define (open-views)
    (clr-call Shell OpenViews '()))

  (define (set-view fn)
    (clr-call Shell SetView '() fn))

  (define view (make-parameter '()))

  (trace-define-syntax gen-ops
    (lambda (e)
      (clr-using IronScheme.VisualStudio)
      (let ((ops (clr-call Shell GetOperations '())))
        (syntax-case e ()
          [(x)
            #`(begin
                (clr-using Microsoft.VisualStudio.Text.Operations)
                #,@(map
                    (lambda (op)
                      (syntax-case (datum->syntax #'open-views op) ()
                        [(clr-type clr-name name args ...)
                          #'(begin
                              (define (name args ...) (clr-call clr-type clr-name (view) args ...))
                              (export name))]))
                    ops))]))))

  (gen-ops)
)
